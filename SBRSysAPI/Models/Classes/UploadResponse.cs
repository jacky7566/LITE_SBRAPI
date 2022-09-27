using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SBRSysAPI.Models.Classes
{
    public class UploadResponse
    {
        public string FilePath { get; set; }
        public string Description { get; set; }
        public string DownloadLink { get; set; }
        public string ContentTypes { get; set; }
        public string FileName { get; set; }
        public Guid AttachHeaderId { get; set; }
        public string TempFileName { get; set; }
    }
}