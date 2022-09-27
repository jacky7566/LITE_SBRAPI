using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Models.Classes
{
    public class TBL_SBR_TFDIES
    {
        public System.Guid Id { get; set; }
        public int Seq { get; set; }
        public int AutoFocusDie { get; set; }
        public int NonAutoFocusDie { get; set; }
    }
}