using SBRSysAPI.Models;
using SBRSysAPI.Models.Classes;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using SystemLibrary.Utility;

namespace SBRSysAPI.Utils
{
    public class SBROperHelper
    {
        public static bool SBRApprovalTransaction(TBL_SBR_RD_Header header)
        {
            if (header.Status == 2) //Submit
            {
                return SendApprovalMail("RD", header, false);
            }
            else if (header.Status == 99) //Reject
            {
                return RejectToCreator(header);
            }
            else if (header.Status == 3) //Approved
            {
                return SendApprovalMail("RD", header, true);
            }

            return false;
        }

        private static string _DefaultAdminReveiver = ConfigurationManager.AppSettings["receiveMails"];
        public static bool RejectToCreator(TBL_SBR_RD_Header header)
        {
            string subjectDet = string.Format("{0} - Version {1} [{2}]", header.SBR_Num, header.Version, header.Wafer_ID);
            string rejectContent = BuildSBR_RD_RejMailContent(header.Id.ToString(), subjectDet, header.LastUpdated_By, header.ApprComment);
            grading_devEntities db = new grading_devEntities();
            List<string> accList = new List<string>();
            accList.Add(header.Created_By);
            accList.Add(header.LastUpdated_By);
            accList.Distinct();
            var emailList = db.Tbl_General_Users.Where(r => accList.Contains(r.UserName)).
                Select(r => r.Email).Distinct().ToList();
            //Add PM mail list to Receiver
            if (string.IsNullOrEmpty(header.Requestor) == false)
            {
                emailList.Add(string.Format("{0}@Lumentum.com", header.Requestor));
            }

            if (emailList != null && emailList.Count() > 0)
            {
                return MailHelper.SendMail(string.Empty, emailList, "SBR Rejected Notice - " + subjectDet,
                    rejectContent, false, null, false);
            }
            else
            {
                emailList = _DefaultAdminReveiver.Split(';').ToList();
                return MailHelper.SendMail(string.Empty, emailList,
                    "SBR Rejected Notice [Creator Not Found] - " + subjectDet,
                    rejectContent, false, null, false);
            }
        }

        private static bool SendApprovalMail(string apprType, TBL_SBR_RD_Header header, bool isApproved)
        {
            string subjectDet = string.Format("{0} - Version {1} [{2}]", header.SBR_Num, header.Version, header.Wafer_ID);
            string sbrApprCont = string.Empty;
            if (isApproved)
                sbrApprCont = BuildSBR_RD_ApprovedMailContent(header.Id.ToString(), subjectDet, header.LastUpdated_By, header.ApprComment);
            else
                sbrApprCont = BuildSBR_RD_AppMailContent(header, subjectDet);
            SBREntities db = new SBREntities();

            //Add for NPI approval
            var lotAppType = "Approver";
            if (header.LotType == "MP Lot")
                lotAppType = "MP_Approver";
            var rdApprList = db.TBL_SBR_Lookup.Where(r => r.LkType == lotAppType
            && r.LkKey == apprType && r.Status == 1).Select(r => r.LkValue).Distinct().ToList();

            //Add PM mail list to Receiver
            if (string.IsNullOrEmpty(header.Requestor) == false)
            {
                rdApprList.Add(string.Format("{0}@Lumentum.com", header.Requestor));
            }

            if (rdApprList != null && rdApprList.Count() > 0)
            {
                return MailHelper.SendMail(string.Empty, rdApprList, "SBR Approval Notice - " + subjectDet, sbrApprCont, false, null, false);
            }
            else
            {
                rdApprList = _DefaultAdminReveiver.Split(';').ToList();
                return MailHelper.SendMail(string.Empty, rdApprList,
                    "SBR Approval Notice [Approver Not Found] - " + subjectDet,
                    sbrApprCont, false, null, false);

            }
        }

        //Approval Notice
        private static string BuildSBR_RD_AppMailContent(TBL_SBR_RD_Header header, string sbrNumVer)
        {
            string sbrURL = ConfigurationManager.AppSettings["FrontEndURL"];
            StringBuilder mailCont = new StringBuilder();
            string url = string.Format(@"{0}/detail/{1}", sbrURL, header.Id.ToString());
            mailCont.Append("Hi Approvers: ");
            mailCont.AppendLine();
            mailCont.AppendFormat("Please check the SBR ticket: {0} as below link! Thanks.", sbrNumVer);
            mailCont.AppendLine();
            mailCont.AppendFormat("Created By: {0}", GetUserNameByAcc(header.Created_By));
            mailCont.AppendLine();
            mailCont.AppendFormat("Lot ID: {0}", header.Wafer_ID);
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Link: {0}", url);
            return mailCont.ToString();
        }

        //Rejected
        private static string BuildSBR_RD_RejMailContent(string sbrId, string sbrNumVer, string lastUpdatedBy, string apprCmmt)
        {
            string sbrURL = ConfigurationManager.AppSettings["FrontEndURL"];
            StringBuilder mailCont = new StringBuilder();
            string url = string.Format(@"{0}/detail/{1}", sbrURL, sbrId);
            mailCont.Append("Hi! ");
            mailCont.AppendLine();
            mailCont.AppendFormat("Your SBR ticket: {0} was rejected by {1}! ", sbrNumVer, GetUserFullName(lastUpdatedBy));
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Approval comments: {0}", apprCmmt);
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Please check the SBR ticket: {0} as below link! Thanks.", sbrNumVer);
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Link: {0}", url);
            return mailCont.ToString();
        }

        //Approved
        private static string BuildSBR_RD_ApprovedMailContent(string sbrId, string sbrNumVer, string lastUpdatedBy, string apprCmmt)
        {
            string sbrURL = ConfigurationManager.AppSettings["FrontEndURL"];
            StringBuilder mailCont = new StringBuilder();
            string url = string.Format(@"{0}/detail/{1}", sbrURL, sbrId);
            mailCont.Append("Hi! ");
            mailCont.AppendLine();
            mailCont.AppendFormat("Your SBR ticket: {0} was approved by {1}! ", sbrNumVer, GetUserFullName(lastUpdatedBy));
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Approval comments: {0}", string.IsNullOrEmpty(apprCmmt) == false ? apprCmmt : "NA");
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Please check the SBR ticket: {0} as below link! Thanks.", sbrNumVer);
            mailCont.AppendLine();
            mailCont.AppendLine();
            mailCont.AppendFormat("Link: {0}", url);
            return mailCont.ToString();
        }

        private static string GetUserFullName(string userID)
        {
            grading_devEntities db = new grading_devEntities();
            var res = db.Tbl_General_Users.Where(r => r.UserName == userID).FirstOrDefault();
            if (res != null)
            {
                var mailArry = res.Email.Split('@');
                if (mailArry.Count() > 1)
                    return mailArry[0];
                else
                    return userID;
            }
            else return userID;
        }

        public static string GetCostTime(TBL_SBR_TestFlow testFlow)
        {
            List<TBL_SBR_TestFlow> tfList = new List<TBL_SBR_TestFlow>() { testFlow };
            string costTime = "0";
            string testType = testFlow.TestType.Trim();
            SBREntities db = new SBREntities();
            var attrList = (from tf in tfList
                            join head in db.TBL_SBR_RD_Header on tf.SBR_RD_Header_Id equals head.Id
                            join attach in db.TBL_SBR_ATTACH on new { head.Id, tf.TestMap }
                            equals new { Id = attach.SBR_Id, TestMap = attach.FileOrigName }
                            join attr in db.TBL_SBR_ATTACH_ATTR on attach.Id equals attr.HeaderId
                            where tf.Id == testFlow.Id
                            select attr).ToList();

            if (attrList != null && attrList.Count() > 0)
            {
                int aFDieCnt = 0;
                int nonAfDieCnt = 0;

                if (testType.Equals("LIV/WL") || testType.Equals("LIV/WL/RIV"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_LIV").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                }
                else if (testType.Equals("NF/M2") || testType.Equals("NF"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_NF").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                    var afItem = attrList.Where(r => r.AttrKey == "MODE_AUTOFOCUS;MODE_M2").FirstOrDefault();
                    if (afItem != null)
                        aFDieCnt = int.Parse(afItem.AttrVal);
                    nonAfDieCnt = nonAfDieCnt - aFDieCnt;
                }
                else if (testType.Equals("FF"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_FF").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                }
                else
                {
                    //Invalid Test Type: [testType]
                    return costTime;
                }
                costTime = CalculateCostTime(testFlow, aFDieCnt, nonAfDieCnt);
            }
            else
            {
                //Missing TestMap Information
            }
            return costTime;
        }

        private static double GetCostTimeVariable(TBL_SBR_TestFlow testFlow)
        {
            double variable = 0.0;
            string lkKey = string.Empty;
            if (testFlow.Sampling_Rate.Equals("100%"))
            {
                lkKey = string.Format("{0}_100", testFlow.TestType);
            }
            else
            {
                lkKey = string.Format("{0}_Others", testFlow.TestType);
            }

            using (SBREntities db = new SBREntities())
            {
                var lkItem = db.TBL_SBR_Lookup.Where(r => r.LkType == "CT_TestXSampR" && r.LkKey == lkKey).FirstOrDefault();
                if (lkItem != null)
                {
                    double.TryParse(lkItem.LkValue, out variable);
                }
            }

            return variable;
        }

        private static string CalculateCostTime(TBL_SBR_TestFlow testFlow, int aFDieCnt, int nonAfDieCnt)
        {
            double costTime = 0.0;
            double variable = 0.0;
            variable = GetCostTimeVariable(testFlow);
            costTime = testFlow.WaferNum * (1440 + nonAfDieCnt * variable + 10 * aFDieCnt) / 3600;
            //costTime = Math.Round(costTime, 3);
            return costTime.ToString();
        }

        public static string GetUserNameByAcc(string account) //ex: lic67888
        {
            grading_devEntities db = new grading_devEntities();
            var userName = account;
            var item = db.Tbl_General_Users.Where(r => r.UserName == account).FirstOrDefault();
            if (item != null)
                userName = item.Email.Split('@')[0].ToString();
            return userName;
        }

        public static TBL_SBR_RD_Header CalculateCostAmt(Guid sbr_header_id, int livTCnt = 1, int nfTCnt = 1, int ffTCnt = 1, string lotType = null)
        {
            TBL_SBR_RD_Header sbrHeader = new TBL_SBR_RD_Header();
            if (string.IsNullOrEmpty(lotType)) lotType = "NPI Lot";
            try
            {
                using (SBREntities db = new SBREntities())
                {
                    sbrHeader = db.TBL_SBR_RD_Header.Where(r => r.Id == sbr_header_id && r.Status == 1).FirstOrDefault();
                    var sbrTfList = db.TBL_SBR_TestFlow.Where(r => r.SBR_RD_Header_Id == sbr_header_id && r.Status == 1).ToList();
                    if (sbrHeader != null)
                    {
                        if (sbrTfList != null && sbrTfList.Count() > 0)
                        {
                            Dictionary<string, int> tfGroupCntDic = GetTfGroupDicByList(sbrTfList); //LIV, NF, FF group

                            //Cost Per Hour
                            if (sbrHeader.LotType == null)
                                sbrHeader.LotType = "NPI Lot";
                            double cph = GetCostPerHourByLotType(lotType == null ? sbrHeader.LotType : lotType); //Default                                                                                                                          
                            //Flow Cnt
                            int flCnt = sbrTfList.Count();
                            //Wafer Cnt
                            int wfCnt = sbrTfList.Max(r => r.WaferNum);
                            //Setup Time
                            double setupTime = 2 + 1.75 * (livTCnt + nfTCnt + ffTCnt) + 1.75
                                * (tfGroupCntDic["LIV"] * livTCnt + tfGroupCntDic["NF"] * nfTCnt + tfGroupCntDic["FF"] * ffTCnt)
                                + 0.5 * wfCnt * flCnt;
                            //Theoritical Test Time(Hour)
                            double ttTime = sbrTfList.Sum(r => double.Parse(r.TestTime));
                            //Cost Amount
                            var costAmt = decimal.Parse(((setupTime + ttTime) * cph).ToString());
                            sbrHeader.CostAmt = costAmt;
                        }
                        else
                        {
                            sbrHeader.CostAmt = 0;
                        }
                        sbrHeader.Last_Updated_Date = DateTime.Now;
                    }
                }
            }
            catch (DbUpdateException e)
            {
                LogHelper.WriteLine(e.StackTrace);
                throw e;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                throw ex;
            }
            return sbrHeader;
        }

        private static Dictionary<string, int> GetTfGroupDicByList(List<TBL_SBR_TestFlow> sbrTfList)
        {
            Dictionary<string, int> tfGroupDic = new Dictionary<string, int>(); //LIV, NF, FF group
            tfGroupDic.Add("LIV", 0);
            tfGroupDic.Add("NF", 0);
            tfGroupDic.Add("FF", 0);
            using (SBREntities db = new SBREntities())
            {
                try
                {
                    var tFLkList = db.TBL_SBR_Lookup.Where(r => r.LkType == "TestFlow" && r.Status == 1).ToList();
                    if (tFLkList != null && tFLkList.Count() > 0)
                    {
                        if (sbrTfList != null)
                        {
                            foreach (var tfItem in sbrTfList)
                            {
                                var group = tFLkList.Where(r => r.LkValue == tfItem.TestType).FirstOrDefault();
                                if (group != null)
                                {
                                    if (tfGroupDic.Where(r => r.Key.Contains(group.LkKey)).Count() > 0)
                                        tfGroupDic[group.LkKey]++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return tfGroupDic;
        }

        private static double GetCostPerHourByLotType(string lotType)
        {
            double costPerHour = 42; //Default
            try
            {
                using (SBREntities db = new SBREntities())
                {
                    var lkItem = db.TBL_SBR_Lookup.Where(r => r.LkType == "LotType"
                    && r.LkKey == lotType && r.Status == 1).FirstOrDefault();
                    if (lkItem != null)
                    {
                        costPerHour = double.Parse(lkItem.LkValue);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return costPerHour;
        }

        public static HttpResponseMessage LogAndResponse(HttpContent hc, HttpStatusCode hsc = HttpStatusCode.OK,
            string message = "", Exception ex = null)
        {
            var resp = new HttpResponseMessage(hsc);
            if (string.IsNullOrEmpty(message) == false)
            {
                LogHelper.WriteLine(message);
                resp.Content = new StringContent(message);
            }
            else
            {
                resp.Content = hc;
            }

            return resp;
        }

        public static void GetFocusDiesFromTestFlow(TBL_SBR_TestFlow testFlow, ref int nonAfDieCnt, ref int aFDieCnt)
        {
            List<TBL_SBR_TestFlow> tfList = new List<TBL_SBR_TestFlow>() { testFlow };
            try
            {
                SBREntities db = new SBREntities();
                var attrList = (from tf in tfList
                                join head in db.TBL_SBR_RD_Header on tf.SBR_RD_Header_Id equals head.Id
                                join attach in db.TBL_SBR_ATTACH on new { head.Id, tf.TestMap }
                                equals new { Id = attach.SBR_Id, TestMap = attach.FileOrigName }
                                join attr in db.TBL_SBR_ATTACH_ATTR on attach.Id equals attr.HeaderId
                                where tf.Id == testFlow.Id
                                select attr).ToList();

                string testType = testFlow.TestType.Trim();
                if (testType.Equals("LIV/WL") || testType.Equals("LIV/WL/RIV"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_LIV").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                }
                else if (testType.Equals("NF/M2") || testType.Equals("NF"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_NF").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                    var afItem = attrList.Where(r => r.AttrKey == "MODE_AUTOFOCUS;MODE_M2").FirstOrDefault();
                    if (afItem != null)
                        aFDieCnt = int.Parse(afItem.AttrVal);
                    nonAfDieCnt = nonAfDieCnt - aFDieCnt;
                }
                else if (testType.Equals("FF"))
                {
                    var item = attrList.Where(r => r.AttrKey == "Enable_FF").FirstOrDefault();
                    if (item != null)
                        nonAfDieCnt = int.Parse(item.AttrVal);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static List<TBL_SBR_DYNAMIC_RESLT> GetDynamicResultByTestFlowId(TBL_SBR_TestFlow testFlow)
        {
            List<TBL_SBR_TestFlow> tfList = new List<TBL_SBR_TestFlow>() { testFlow };
            try
            {
                SBREntities db = new SBREntities();
                var dynamicList = (from tf in tfList
                                   join dr in db.TBL_SBR_Dynamic_ColumnResult on tf.Id equals dr.TestFlowId
                                   join dl in db.TBL_SBR_Dynamic_ColumnLookup on dr.DynamicColumnLookupId equals dl.Id
                                   select new TBL_SBR_DYNAMIC_RESLT()
                                   {
                                       Id = tf.Id,
                                       Seq = tf.Seq,
                                       GroupName = dl.GroupName,
                                       LabelName = dl.LabelName,
                                       DefaultValue = dl.DefaultValue,
                                       Value = dr.Value
                                   }).ToList();

                return dynamicList;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static List<TBL_SBR_TestFlow_Steps> GetTestFlowStepsByTestFlowId(TBL_SBR_TestFlow testFlow)
        {
            //List<TBL_SBR_TestFlow> tfList = new List<TBL_SBR_TestFlow>() { testFlow };
            try
            {
                SBREntities db = new SBREntities();
                var res = (from ts in db.TBL_SBR_TestFlow_Steps
                           where ts.Testflow_Id == testFlow.Id
                           select ts).OrderBy(r => r.Step).ToList();

                return res;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}