using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Models.Classes
{
    public class TBL_SBR_ATTACH_DETAILS
    {
        public System.Guid Id { get; set; }
        public System.Guid SBR_Id { get; set; }
        public string AttachType { get; set; }
        public string FileRealPath { get; set; }
        public string FileOrigName { get; set; }
        public string FileTempName { get; set; }
        public Nullable<int> Status { get; set; }
        public System.DateTime Creation_Date { get; set; }
        public System.DateTime Last_Updated_Date { get; set; }
        public string Attribute { get; set; }
        public string Created_By { get; set; }
        public string LastUpdated_By { get; set; }
        public List<TBL_SBR_ATTACH_ATTR> AttrList { get; set; }
    }
}