using SBRSysAPI.Models;
using SBRSysAPI.Utils.MapValidator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Utils
{
    public class SBRAttachHelper
    {
        SBREntities _db = new SBREntities();
        public TBL_SBR_ATTACH CreateSBRAttach(string fileName, string tempFileName, string savePath, string mapType, Guid sbr_Id, string attr)
        {
            DateTime dtNow = DateTime.Now;
            TBL_SBR_ATTACH attach = null;
            //if (mapType == MapType.EmitMap.ToString())
            //{
            //    attach = _db.TBL_SBR_ATTACH.Where(r => r.SBR_Id == sbr_Id & r.AttachType == mapType & r.Attribute == emitCnt).FirstOrDefault();
            //}
            //else
            //{ 
            //    attach = _db.TBL_SBR_ATTACH.Where(r => r.SBR_Id == sbr_Id & r.AttachType == mapType 
            //    & r.FileOrigName == fileName & r.Attribute == emitCnt).FirstOrDefault();
            //}
            attach = _db.TBL_SBR_ATTACH.Where(r => r.SBR_Id == sbr_Id & r.AttachType == mapType
                    & r.FileOrigName == fileName & r.Attribute == attr).FirstOrDefault();

            if (attach != null)
            {
                attach.LastUpdated_By = "SBRSys";
                attach.Last_Updated_Date = dtNow;
                attach.FileOrigName = fileName;
                attach.FileTempName = tempFileName;
                attach.FileRealPath = savePath;
                attach.Attribute = attr;                
                _db.Entry(attach);
            }
            else
            {
                attach = new TBL_SBR_ATTACH()
                {
                    Id = Guid.NewGuid(),
                    SBR_Id = sbr_Id,
                    AttachType = mapType,
                    FileOrigName = fileName,
                    FileTempName = tempFileName,
                    FileRealPath = savePath,
                    Creation_Date = dtNow,
                    Last_Updated_Date = dtNow,
                    Created_By = "SBRSys",
                    LastUpdated_By = "SBRSys",
                    Status = 1,
                    Attribute = attr
                };
                _db.TBL_SBR_ATTACH.Add(attach);
            }

            if (_db.SaveChanges() > 0)
            {
                return attach;
            }
            else
                return null;
        }
    }
}