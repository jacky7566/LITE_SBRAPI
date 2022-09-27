using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Models.Classes
{
    public class TBL_SBR_DYNAMIC_RESLT
    {
        public System.Guid Id { get; set; }
        public int Seq { get; set; }
        public string GroupName { get; set; }
        public string LabelName { get; set; }
        public string DefaultValue { get; set; }
        public string Value { get; set; }
    }
}