using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Models.Classes
{
    public class SBR_ATTACH_GRP
    {
        public string EmitCount { get; set; }
        public List<TBL_SBR_ATTACH_DETAILS> list { get; set; }
    }
}