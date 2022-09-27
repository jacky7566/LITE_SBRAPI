using SBRSysAPI.Controllers;
using SBRSysAPI.Models;
using SBRSysAPI.Models.Classes;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SystemLibrary.Utility;

namespace SBRSysAPI.Utils.MapValidator
{
    public enum MapType { EmitMap, TestMap, TpDefMap };
    public class MapValidator
    {
        SBREntities _db = new SBREntities();
        List<TBL_SBR_Lookup> _lookUpList = new List<TBL_SBR_Lookup>();
        private string _FilePath;
        private string _MapType;
        private Guid _SBRHeaderId;
        private Guid _SBRAttachId;
        private string _EmitterCount;
        public Dictionary<string, string> _PassInfoDic;
        private TBL_SBR_ATTACH _SBRAttach;

        public MapValidator(MapValidateClass mapValItem)
        {
            _SBRHeaderId = Guid.Parse(mapValItem.SBR_Id);
            _SBRAttachId = Guid.Parse(mapValItem.AttachHeaderId.ToUpper());
            _EmitterCount = mapValItem.EmitCount;
            this._lookUpList = _db.TBL_SBR_Lookup.ToList();
            this._PassInfoDic = new Dictionary<string, string>();
        }

        public string Validate(string filePath, string mapType)
        {
            Dictionary<string, string> errDic = new Dictionary<string, string>();
            StringBuilder sb = new StringBuilder();

            try
            {
                _SBRAttach = (from item in _db.TBL_SBR_ATTACH
                              where item.Id == _SBRAttachId
                              select item).FirstOrDefault();
                if (_SBRAttach != null)
                {
                    this._FilePath = filePath;
                    this._MapType = mapType.ToString();

                    FileInfo fi = new FileInfo(this._FilePath);
                    if (fi.Exists)
                    {
                        //1. Check File Format
                        var fileFormatRes = this.CheckFileFormatByType(fi);
                        if (string.IsNullOrEmpty(fileFormatRes) == false)
                            errDic.Add("WrongContentHeader", fileFormatRes);
                        else
                        {
                            //2. Read File Content
                            errDic.AddRange(this.StartFileValidate());
                            //if (System.Enum.IsDefined(typeof(MapType), mapType))
                            //{
                            //    errDic.AddRange(this.StartFileValidate());
                            //}
                            //else //No need to check content, Validated as Y
                            //{
                            //    errDic.Add("IsValid", "Y");
                            //}
                        }
                    }
                    else
                    {
                        errDic.Add("WrongContentHeader", string.Format("File not found! {0}", this._FilePath));
                    }
                }
                else
                {
                    errDic.Add("WrongContentHeader", string.Format("File not found! {0}", this._FilePath));
                }

                if (errDic.Any())
                {
                    sb.Append(DictionaryToJson(errDic));
                    if (this.SetAttachDisableIfFail() == false)
                    {
                        errDic.Add("DisableAttachDataFail", _SBRAttachId.ToString());
                    }
                    errDic.Add("IsValid", "N");
                    //FileController file = new FileController();
                    //file.DelFileByAttachId(_SBRHeaderId);
                }
                else sb.Append(DictionaryToJson(this._PassInfoDic));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                throw ex;
            }
            
            return sb.ToString();
        }

        #region Start File Validate
        private Dictionary<string, string> StartFileValidate()
        {
            Dictionary<string, string> errorDic = new Dictionary<string, string>();
            List<Dictionary<string, string>> listDic = new List<Dictionary<string, string>>();
            if (this._MapType == "EmitMap")
            {
                listDic = ExcelOperator.GetCsvDataToDic(this._FilePath, true);
            }
            else if (this._MapType == "TpDefMap")
            {
                listDic = ExcelOperator.GetCsvDataToDicReverse(this._FilePath);
            }
            else if (this._MapType == "TestMap")
            {
                listDic = ExcelOperator.GetCsvDataToDic(this._FilePath);
            }
            //File Header Check
            //1. Get file header by mapType
            var headerStr = this.GetTBLLkUpByTypeKey(this._MapType, "Header").FirstOrDefault();
            //2. Get header from retrive excel dictionary
            if (headerStr != null)
            {
                string headerVal = headerStr.LkValue;
                string dicHeader = string.Empty;
                Regex headerReg = new Regex(headerVal.ToUpper());
                
                //Append extra header from Test Map
                if (this._MapType.Equals("TpDefMap"))
                    dicHeader = listDic[0].Keys.Aggregate((res, next) => res + (next.StartsWith("Type") == false ? "," + next : string.Empty)).ToUpper();
                else
                    dicHeader = listDic[0].Keys.Aggregate((res, next) => res + "," + next).ToUpper();

                //3. Check Header is correct
                if (headerReg.IsMatch(dicHeader.Trim()) == false)
                {
                    errorDic.Add("WrongContentHeader", string.Format(Resources.Message_en.Map_FileContentError, dicHeader));
                    return errorDic;
                }
            }
            //4. Check content
            errorDic = this.ValidateMapContentByType(listDic);

            return errorDic;
        }
        #endregion

        #region Validate Content Function (Emitter Map -> TypeDef Map -> Test Map)
        private Dictionary<string, string> ValidateMapContentByType(List<Dictionary<string, string>> listDic)
        {
            string emitMapCnt = string.Empty;
            string errOpt = string.Empty;
            string isValid = "Y";
            Dictionary<string, string> errorDic = new Dictionary<string, string>();

            if (this._MapType.Equals("EmitMap"))
            {
                var fileEmitCnt = _SBRAttach.FileOrigName.Split('_')[3].TrimEnd('e');
                if (_EmitterCount.Equals(fileEmitCnt) == false)
                {                    
                    errorDic.Add("EmitCntMisMatch", "Emitter count mis-match!");
                    LogHelper.WriteLine("Input: " + _EmitterCount);
                    LogHelper.WriteLine("File: " + fileEmitCnt);
                }

                int fnEmiCnt = 0;
                int.TryParse(_EmitterCount, out fnEmiCnt);
                errOpt = this.ValidateEmitterMap(listDic, fnEmiCnt);
                if (errOpt.Count() == 0)
                {
                    //Record Emit Count from EmitMap
                    this.SaveTBLSBRAttr(MapType.EmitMap.ToString(), "EmitCount", fnEmiCnt.ToString(), _EmitterCount);
                    //Record Emitter Map File Name if every check has passed
                    //Get File Extenstion
                    string ext = new FileInfo(this._FilePath).Extension;
                    this.SaveTBLSBRAttr(MapType.EmitMap.ToString(), "EmitterMapFileName", _SBRAttach.FileOrigName.Replace(ext, ""), _EmitterCount);                    
                }
            }
            else if (this._MapType.Equals("TpDefMap"))
            {
                errOpt = this.ValidateTypeDefMap(listDic);
                if (errOpt.Count() == 0)
                {
                    //Record Types
                    var typeList = listDic[0].Where(r => r.Key.StartsWith("Type")).Select(r => r.Key).OrderBy(r => r).ToList();
                    this.SaveTBLSBRAttr(MapType.TpDefMap.ToString(), "Types", typeList.Aggregate((res, next) => res + "," + next), _EmitterCount);
                }
            }
            else if (this._MapType.Equals("TestMap"))
            {
                errOpt = this.ValidateTestMap(listDic);
                
                if (errOpt.Count() == 0)
                {
                    if (string.IsNullOrEmpty(_EmitterCount) == false)
                    {
                        //this.SaveTBLSBRAttr(MapType.TestMap, "Types", this._TypeList.Distinct().Aggregate((res, next) => res + "," + next), emitMapCnt);
                        //Record Die Count Information
                        this.VTM_SaveDiesInfo(listDic, _EmitterCount);
                    }
                    else errorDic.Add("MissEmitCntInfo", "Missing 'emitter count' infomation!");
                }
            }
            //else
            //{
            //    errorDic.Add("NoSuchMapType", "No such map type found!");
            //}

            if (string.IsNullOrEmpty(errOpt) == false)
            {
                errorDic.Add("ValidateMapContent", errOpt);
                isValid = "N";
            }

            this.SaveTBLSBRAttr(this._MapType, "IsValid", isValid, _EmitterCount);
            //if (this.UpdateEmitCntToAttach() == false)
            //{
            //    errorDic.Add("Update Emitter Count Error", "Attachmend Id: " + _SBRAttachId);
            //}

            return errorDic;
        }
        #endregion

        #region Validate Type Def Map Functions
        private string ValidateTypeDefMap(List<Dictionary<string, string>> list)
        {
            StringBuilder res = new StringBuilder();
            var totalGroups = int.Parse(list[0]["TotalGroups"].ToString());
            var totalTypes = int.Parse(list[0]["TotalTypes"].ToString());
            List<string> indexList = new List<string>();
            List<string> groupList = new List<string>();
            int index = 1;
            string idxVal = string.Empty;
            string grpVal = string.Empty;
            var grpsArry = this.GetTBLLkUpByTypeKey(this._MapType, "Groups").FirstOrDefault().LkValue.Split(',');
            var typesArry = this.GetTBLLkUpByTypeKey(this._MapType, "TypeVals").FirstOrDefault().LkValue.Split(',');

            //1. Check Types Count match TotalTypes
            var realTypesCnt = list[0].Where(r => r.Key.StartsWith("Type")).ToList().Count();
            if (realTypesCnt.Equals(totalTypes) == false)
            {
                res.Append("'TypeX' amout mis-match TotalTypes");
                res.Append(";");
            }

            foreach (Dictionary<string, string> dic in list)
            {
                //2. Check Index Data
                if (dic.ContainsKey("Index"))
                {
                    idxVal = dic["Index"].ToString();
                    //Check Index Sequence
                    if (indexList.Count() > 0
                        && this.CmpCurAndNextIsSeq(indexList[indexList.Count() - 1], idxVal) == false)
                    {
                        res.AppendFormat("Index sequence error! Column {0} = {1} ; Column {2} = {3}",
                            index, indexList[indexList.Count() - 1], index + 1, idxVal);
                        res.Append(";");
                    }
                    indexList.Add(dic["Index"].ToString());
                }
                else
                {
                    res.AppendFormat("Missing 'Index' value on column: {0}", index + 1);
                    res.Append(";");
                }

                //3. Check Group Data
                if (dic.ContainsKey("Group"))
                {
                    grpVal = dic["Group"].ToString();
                    if (grpsArry.Contains(grpVal) == false)
                    {
                        res.AppendFormat("Out of 'Group' type on column: {0}, value: {1}", index + 1, grpVal);
                        res.Append(";");
                    }
                    groupList.Add(grpVal);
                }

                //4. Check TypeX Data
                var typeList = dic.Where(r => r.Key.StartsWith("Type")).ToList();
                if (typeList != null)
                {
                    foreach (var typeDic in typeList)
                    {
                        if (typesArry.Contains(typeDic.Value) == false)
                        {
                            //All Type rows in the TypeDef file contain only 1 or 0 (Type1)
                            res.AppendFormat("'Type' content should be '1' or '0'; Column: {0}, value: {1}", index + 1, typeDic.Value);
                            res.Append(";");
                        }
                    }
                }

                index++;
            }

            //5. Confirm the GroupList is match the TotalGroups count (Check TotalGroup (row 1) count equals to Group count (row 4))
            var groupCnt = groupList.Distinct().ToList().Count();
            if (groupCnt.Equals(totalGroups) == false)
            {
                res.Append("Group numbers not match Total Groups");
                res.Append(";");
            }

            //6. Check Index count match Emiter Map emitter count
            var typeDefEmitCnt = indexList.Count();

            if (string.IsNullOrEmpty(_EmitterCount))
            {
                res.Append("No emitter data found!");
                res.Append(";");
            }
            else
            {
                if (typeDefEmitCnt.Equals(int.Parse(_EmitterCount)) == false)
                {
                    res.Append("Group numbers not match Total Groups");
                    res.Append(";");
                }
            }
            return res.ToString();
        }
        #endregion

        #region Validate Emitter Map Functions
        private string ValidateEmitterMap(List<Dictionary<string, string>> list, int fileNameEmitCnt)
        {
            StringBuilder res = new StringBuilder();
            var indexList = list.Select(r => r["INDEX"]).ToList();
            var xCordList = list.Select(r => r["X"]).ToList();
            var yCordList = list.Select(r => r["Y"]).ToList();
            string curIdx = string.Empty;
            string nextIdx = string.Empty;
            var numRegex = new Regex(@"^-?[0-9].*$");

            //1. Check Index Sequence
            for (int i = 0; i < indexList.Count(); i++)
            {
                if (i == indexList.Count() - 1) break;
                curIdx = indexList[i];
                nextIdx = indexList[i + 1];

                if (numRegex.IsMatch(curIdx) == false)
                {
                    res.Append("Column index content must be number!");
                    res.Append(";");
                }

                if (this.CmpCurAndNextIsSeq(curIdx, nextIdx) == false)
                {
                    res.AppendFormat("Index sequence error! Row {0} = {1} ; Row {2} = {3}", i + 2, curIdx, i + 3, nextIdx);
                    res.Append(";");
                }
            }

            //2. Check Content must be number
            var notMatchX = xCordList.Where(r => numRegex.IsMatch(r) == false).ToList();
            var notMatchY = yCordList.Where(r => numRegex.IsMatch(r) == false).ToList();

            if (notMatchX.Count() > 0)
            {
                res.AppendFormat("Column x content must be number!");
                res.Append(";");
            }

            if (notMatchY.Count() > 0)
            {
                res.AppendFormat("Column y content must be number!");
                res.Append(";");
            }

            //3. Check Emit Count Index same as file name
            if (indexList.Count().Equals(fileNameEmitCnt) == false)
            {
                res.Append("Emiter count mis-match on file name");
                res.Append(";");
            }

            return res.ToString();
        }
        #endregion

        #region Validate Test Map Functions 
        private string ValidateTestMap(List<Dictionary<string, string>> list)
        {
            StringBuilder res = new StringBuilder();
            //1. Get All Type List
            var testMapTypeList = list.Select(r => r["Type"]).Distinct().ToList();
            this.VTM_ChkTypesInfo(testMapTypeList, ref res);

            //2. Check Emitter Mapping column
            var emitMapList = list.Select(r => r["EmitterMapping"]).Distinct().ToList();
            this.VTM_ChkEmitMapInfo(emitMapList, ref res);

            //3. Check Status NF Column
            var nfList = list.Select(r => r["Status_NF"]).Distinct().ToList();
            this.VTM_ChkStsNFInfo(nfList, ref res);

            //4. Check Column D,E,F,G can only be 0 or 1
            var defgList = list.SelectMany(r => new[] { r["IsAlignmentKey"], r["Enable_LIV"], r["Enable_NF"], r["Enable_FF"] }).Distinct().ToList();
            this.VTM_ChkListIsRegex(@"^[0-1]$", defgList, ref res, "D/E/F/G");

            //5. Check Column H,J can only be IOP
            var hjList = list.SelectMany(r => new[] { r["Status_LIV"], r["Status_FF"] }).Distinct().ToList();
            this.VTM_ChkListIsRegex(@"^IOP$", hjList, ref res, "H/J");

            return res.ToString();
        }

        private void VTM_ChkTypesInfo(List<string> testMapTypeList, ref StringBuilder res)
        {
            string err = string.Empty;
            var tpDefTypeItemList = this.GetTBLAttrByTypeKey(MapType.TpDefMap, "Types");
            if (tpDefTypeItemList.Count() > 0)
            {
                foreach (var item in tpDefTypeItemList)
                {
                    var tpDefTpList = item.AttrVal.Split(',').OrderBy(r => r).ToList();
                    if (testMapTypeList.Except(tpDefTpList).Any())
                    {
                        err = @"Types mis-match with [TypeDef] map!;";
                    }
                    else
                    {
                        err = string.Empty;
                        break;
                    }
                }
                //var tpDefTpList = tpDefTypeItemList.FirstOrDefault().AttrVal.Split(',').OrderBy(r => r).ToList();
                if (string.IsNullOrEmpty(err) == false) res.Append(err);
            }
            else
            {
                res.Append("Missing [Types] information!");
                res.Append(";");
            }
        }

        private void VTM_ChkEmitMapInfo(List<string> emitMapList, ref StringBuilder res)
        {
            //Not allow two emitter map setting
            if (emitMapList.Count() > 0)
            {
                if (emitMapList.Count() == 1)
                {
                    var emitMapFileList = this.GetTBLAttrByTypeKey(MapType.EmitMap, "EmitterMapFileName");
                    if (emitMapFileList != null)
                    {
                        if (emitMapFileList.Select(r=> r.AttrVal).Contains(emitMapList.FirstOrDefault()) == false)
                        {
                            res.Append("Emitter filename mismatch!");
                            res.Append(";");
                        }
                    }
                    else
                    {
                        res.Append("No emitter map found!");
                        res.Append(";");
                    }
                }
                else
                {
                    res.Append("Emitter Mapping can not have duplicate input!");
                    res.Append(";");
                }
            }
            else
            {
                res.Append("Emitter Mapping can not be empty!");
                res.Append(";");
            }            
        }

        private void VTM_ChkStsNFInfo(List<string> nfList, ref StringBuilder res)
        {
            if (nfList.Count() > 0)
            {
                var lkNFList = this.GetTBLLkUpByTypeKey(this._MapType, "StatusNFs");
                if (lkNFList.Count() > 0)
                {
                    var lkNFStrList = lkNFList.Select(r => r.LkValue).FirstOrDefault().Split(',').ToList();
                    //var lkNotTsMp = lkNFStrList.Except(nfList).ToList();
                    var TsMpNotLk = nfList.Except(lkNFStrList).ToList();
                    if (TsMpNotLk.Any())
                    {
                        res.AppendFormat("Status NF out of spec!");
                        res.Append(";");
                    }
                }
                else
                {
                    res.Append("Lookup value StatusNFs missing!");
                    res.Append(";");
                }
            }
            else
            {
                res.Append("Status_NF can not be empty!");
                res.Append(";");
            }
        }

        private void VTM_ChkListIsRegex(string regStr, List<string> checkList, ref StringBuilder res, string colStr)
        {
            var regex = new Regex(regStr);
            var notMatchList = checkList.Where(r => regex.IsMatch(r) == false).ToList();
            if (notMatchList.Count() > 0)
            {
                res.AppendFormat("Invalid content in column {0}!", colStr);
                res.Append(";");
            }
        }

        private void VTM_SaveDiesInfo(List<Dictionary<string, string>> listDic, string emitCount)
        {
            //1. Count Enable_LIV = 1
            var enbLivList = listDic.Select(r => r["Enable_LIV"]).ToList().Where(r => r == "1").ToList();
            this.SaveTBLSBRAttr(MapType.TestMap.ToString(), "Enable_LIV", enbLivList.Count().ToString(), emitCount);

            //2. Count Enable_FF = 1
            var enbFfList = listDic.Select(r => r["Enable_FF"]).ToList().Where(r => r == "1").ToList();
            this.SaveTBLSBRAttr(MapType.TestMap.ToString(), "Enable_FF", enbFfList.Count().ToString(), emitCount);

            //3. Count All dies -> Enable_NF = 1; # of AF dies -> Status_NF = MODE_AUTOFOCUS/MODE_AUTOFOCUS;MODE_M2, # of non-AF dies -> All dies - AF dies
            var enbNfList = listDic.Select(r => r["Enable_NF"]).ToList().Where(r => r == "1").ToList();
            this.SaveTBLSBRAttr(MapType.TestMap.ToString(), "Enable_NF", enbNfList.Count().ToString(), emitCount);

            var stsNfList = listDic.Select(r => r["Status_NF"]).ToList();
            var nfResDic = stsNfList.GroupBy(r => r).Select(x => new { Id = x, Count = x.Count() }).ToList();
            foreach (var item in nfResDic)
            {
                this.SaveTBLSBRAttr(MapType.TestMap.ToString(), item.Id.Key, item.Count.ToString(), emitCount);
            }
        }
        #endregion

        #region SBR Attribute Transactions
        public bool SaveTBLSBRAttr(string mapType, string key, string val, string typeGroup)
        {
            bool isPass = false;
            DateTime dt = DateTime.Now;
            string attrType = string.Format("{0}{1}", mapType, "_" + typeGroup);
            var oldAttr = _db.TBL_SBR_ATTACH_ATTR.Where(r => r.SBR_Id == _SBRHeaderId 
            & r.AttrType == attrType & r.AttrKey == key & r.HeaderId == _SBRAttachId).FirstOrDefault();

            if (oldAttr != null)
            {
                oldAttr.AttrVal = val;
                oldAttr.LastUpdated_By = "sys";
                oldAttr.Last_Updated_Date = dt;
                _db.Entry(oldAttr);
            }
            else
            {
                TBL_SBR_ATTACH_ATTR tblAttr = new TBL_SBR_ATTACH_ATTR();
                tblAttr.Id = Guid.NewGuid();
                tblAttr.SBR_Id = _SBRHeaderId;
                tblAttr.HeaderId = _SBRAttachId;
                tblAttr.AttrType = attrType;
                tblAttr.AttrKey = key;
                tblAttr.AttrVal = val;
                tblAttr.Created_By = "sys";
                tblAttr.LastUpdated_By = "sys";
                tblAttr.Creation_Date = dt;
                tblAttr.Last_Updated_Date = dt;
                tblAttr.Status = 1;
                _db.TBL_SBR_ATTACH_ATTR.Add(tblAttr);
            }
            
            try
            {
                isPass = _db.SaveChanges() > 0 ? true : false;
                if (isPass) this._PassInfoDic.Add(key, val);
            }
            catch (DbException e)
            {
                LogHelper.WriteLine(e.InnerException.ToString());
                return false;
            }

            return isPass;
        }

        private List<TBL_SBR_ATTACH_ATTR> GetTBLAttrByTypeKey(MapType mapType, string key)
        {
            string attrType = string.Format("{0}_{1}", mapType.ToString(), _EmitterCount);
            var res = _db.TBL_SBR_ATTACH_ATTR.Where(r => r.AttrType == attrType
                            && r.AttrKey == key && r.SBR_Id == _SBRHeaderId).ToList();

            return res;
        }

        private List<TBL_SBR_Lookup> GetTBLLkUpByTypeKey(MapType mapType, string key)
        {
            var res = this._lookUpList.Where(r => r.LkType == mapType.ToString() && r.LkKey == key && r.Status == 1).ToList();

            return res;
        }

        private List<TBL_SBR_Lookup> GetTBLLkUpByTypeKey(string mapType, string key)
        {
            var res = this._lookUpList.Where(r => r.LkType == mapType && r.LkKey == key && r.Status == 1).ToList();

            return res;
        }

        private bool SetAttachDisableIfFail()
        {
            var attr = _db.TBL_SBR_ATTACH_ATTR.Where(r => r.HeaderId == _SBRAttachId).ToList();
            if (attr != null)
                _db.TBL_SBR_ATTACH_ATTR.RemoveRange(attr);
            var attach = _db.TBL_SBR_ATTACH.Where(r => r.Id == _SBRAttachId).FirstOrDefault();
            if (attach != null)
            {
                string filePath = attach.FileRealPath;
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _db.TBL_SBR_ATTACH.Remove(attach);
                    }

                    _db.Entry(attach);
                    return _db.SaveChanges() > 0 ? true : false;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLine(ex.StackTrace);
                    return false;
                }
            }
            else return false;

        }
        #endregion

        #region Utilities
        private string CheckFileFormatByType(FileInfo fi)
        {
            Dictionary<string, string> errorDic = new Dictionary<string, string>();
            //Check File Name by Regular expression
            string fileName = _SBRAttach.FileOrigName;
            var regItem = this.GetTBLLkUpByTypeKey(this._MapType, "FileFormat").FirstOrDefault();
            if (regItem != null)
            {
                Match match = Regex.Match(fileName, regItem.LkValue.Replace("\r\n", ""), RegexOptions.IgnoreCase);
                if (match.Success == false)
                    return Resources.Message_en.Map_FileFormatError;
            }
            return string.Empty;
        }

        private bool CmpCurAndNextIsSeq(string oldStr, string newStr)
        {
            int oldInt = 0;
            int newInt = 0;
            int.TryParse(oldStr.Trim(), out oldInt);
            int.TryParse(newStr.Trim(), out newInt);
            return newInt == oldInt + 1;
        }

        private string DictionaryToJson(Dictionary<string, string> dict)
        {
            if (dict.Any() == false) return string.Empty;
            var entries = dict.Select(d =>
                string.Format("\"{0}\": \"{1}\"", d.Key, string.Join(",", d.Value)));
            return "{" + string.Join(",", entries) + "}";
        }
        #endregion
    }
}