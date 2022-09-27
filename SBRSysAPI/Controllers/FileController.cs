using Newtonsoft.Json.Linq;
using SBRSysAPI.Models;
using SBRSysAPI.Models.Classes;
using SBRSysAPI.Providers;
using SBRSysAPI.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Results;
using SystemLibrary.Utility;

namespace SBRSysAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/MapUploader")]
    public class FileController : ApiController
    {    
        // GET: api/SBRMapValidate
        [HttpGet]
        [Route("GetAttachList/{SBR_Id}")]
        public List<TBL_SBR_ATTACH_DETAILS> Get(Guid SBR_Id)
        {
            SBREntities db = new SBREntities();
            List<TBL_SBR_ATTACH_DETAILS> list = new List<TBL_SBR_ATTACH_DETAILS>();
            list = (from attach in db.TBL_SBR_ATTACH
                    join attr in db.TBL_SBR_ATTACH_ATTR on attach.Id equals attr.HeaderId into newAttr
                    from attr in newAttr.DefaultIfEmpty()
                    where attach.SBR_Id == SBR_Id && attach.Status == 1
                    group new { attach, attr } by new
                    {
                        attach.Id,
                        attach.SBR_Id,
                        attach.AttachType,
                        attach.FileOrigName,
                        attach.FileRealPath,
                        attach.FileTempName,
                        attach.Attribute,
                        attach.Status
                    } into res
                    select new TBL_SBR_ATTACH_DETAILS()
                    {
                        Id = res.Key.Id,
                        AttachType = res.Key.AttachType,
                        SBR_Id = res.Key.SBR_Id,
                        FileOrigName = res.Key.FileOrigName,
                        FileTempName = res.Key.FileTempName,
                        FileRealPath = res.Key.FileRealPath,
                        Attribute = res.Key.Attribute,
                        Status = res.Key.Status,
                        Created_By = res.Select(r => r.attach.Created_By).FirstOrDefault(),
                        Creation_Date = res.Select(r => r.attach.Creation_Date).FirstOrDefault(),
                        LastUpdated_By = res.Select(r => r.attach.LastUpdated_By).FirstOrDefault(),
                        Last_Updated_Date = res.Select(r => r.attach.Last_Updated_Date).FirstOrDefault(),
                        AttrList = res.Select(r => r.attr).ToList()
                    }).OrderBy(r => r.Last_Updated_Date).ToList();

            
            return list;
        }

        [HttpPost]
        [Route("Upload")]
        public async Task<IHttpActionResult> Upload()
        {
            try
            {
                if (!this.Request.Content.IsMimeMultipartContent())
                {
                    LogHelper.WriteLine(HttpStatusCode.UnsupportedMediaType.ToString());
                    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                }
                
                var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
                var exists = Directory.Exists(root);
                if (!exists)
                {
                    Directory.CreateDirectory("App_Data");
                }

                var provider = await Request.Content.ReadAsMultipartAsync<InMemoryMultipartFormDataStreamProvider>(new InMemoryMultipartFormDataStreamProvider());
                var values = new List<string>();

                //access form data
                NameValueCollection formData = provider.FormData;
                var keys = formData.AllKeys;
                foreach(var key in keys)
                {
                    values.Add(formData[key]);
                }

                //access files
                IList<HttpContent> files = provider.Files;
                SBRAttachHelper attachHelper = new SBRAttachHelper();

                List<UploadResponse> uploadResponseList = new List<UploadResponse>();
                UploadResponse ur = null;
                FileInfo fi;
                string newFilePath = string.Empty;
                string newFileName = string.Empty;
                foreach (var content in files)
                {
                    ur = new UploadResponse();
                    var fileName = content.Headers.ContentDisposition.FileName.Trim('\"');
                    var fileBytes = await content.ReadAsByteArrayAsync();

                    var outputPath = Path.Combine(root, fileName);
                    using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        await output.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }
                    //LogHelper.WriteLine(fileName);
                    //Create tempFile
                    if (File.Exists(outputPath))
                    {                        
                        fi = new FileInfo(outputPath);
                        var ext = fi.Extension;
                        newFileName = string.Format("{0}{1}", Guid.NewGuid(), ext);
                        newFilePath = string.Format("{0}\\{1}", fi.DirectoryName, newFileName);
                        File.Move(fi.FullName, newFilePath);
                        File.Delete(fi.FullName);
                    }

                    var attribute = string.Empty;
                    if (formData["MapType"] == "EmitMap" || formData["MapType"] == "TpDefMap" || formData["MapType"] == "TestMap")
                    {
                        var specialInfo = string.Empty;
                        if (fileName.Split('_').Count() > 1)
                            specialInfo = fileName.Split('_')[2] + "_";
                        attribute = specialInfo + formData["EmitCount"] + "e";
                    }

                    TBL_SBR_ATTACH attach = attachHelper.CreateSBRAttach(fileName, newFileName, newFilePath, formData["MapType"], Guid.Parse(formData["SBR_Id"]), attribute);
                    if (attach != null)
                    {
                        ur.FileName = fileName;
                        ur.TempFileName = newFileName;
                        ur.FilePath = newFilePath;
                        ur.ContentTypes = content.Headers.ContentType.MediaType;
                        ur.Description = provider.FormData["description"];
                        ur.AttachHeaderId = attach.Id;
                        uploadResponseList.Add(ur);
                    }
                    else return this.InternalServerError();
                }

                return this.Ok(uploadResponseList);
            }
            catch (DbEntityValidationException ex)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(ex.StackTrace)
                });
            }
            catch (Exception e)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(e.StackTrace)
                });
            }
        }

        [HttpGet]
        [Route("download")]
        public async Task<IHttpActionResult> Download(string fileName)
        {
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            var exists = Directory.Exists(root);
            if (!exists)
            {
                Directory.CreateDirectory("App_Data");
            }

            var filePath = Path.Combine(root, fileName);
            try
            {
                if (!File.Exists(filePath))
                {
                    return this.NotFound();
                }

                var fileStream = new FileStream(filePath, FileMode.Open);
                var content = new StreamContent(fileStream);
                //var content = new ByteArrayContent(File.ReadAllBytes(filePath));
                //content.Headers.ContentType = new MediaTypeHeaderValue("application/ms-excel");
                content.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.GetMimeMapping(fileName));

                content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = fileName
                };
                return new ResponseMessageResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                });
            }
            catch (Exception e)
            {
                throw new HttpResponseException(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(e.Message)
                });
            }
        }

        [HttpPost]
        [Route("DelFileByAttachId/{AttachHeaderId}")]
        public string DelFileByAttachId(Guid AttachHeaderId)
        {
            SBREntities db = new SBREntities();
            var attr = db.TBL_SBR_ATTACH_ATTR.Where(r => r.HeaderId == AttachHeaderId).ToList();
            db.TBL_SBR_ATTACH_ATTR.RemoveRange(attr);
            var attach = db.TBL_SBR_ATTACH.Where(r => r.Id == AttachHeaderId).FirstOrDefault();
            var rtnMsg = string.Format("File: {0} has deleted!", attach.FileOrigName);
            if (attach != null)
            {
                string filePath = attach.FileRealPath;
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        db.TBL_SBR_ATTACH.Remove(attach);
                    }                        
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLine(ex.StackTrace);
                    return ex.StackTrace;
                }
            }
            try
            {
                if (db.SaveChanges() == 0)
                    return "Fail;" + rtnMsg;
            }
            catch (DbEntityValidationException ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                return ex.StackTrace;
            }

            return "Pass;" + rtnMsg;
        }

        [HttpPost]
        [Route("DelFileBySBRIdAndEmitCnt/{SBR_Id}/{EmitCnt}/{IsDelTF}")]
        public string DelFileBySBRIdAndEmitCnt(Guid SBR_Id, string EmitCnt, bool IsDelTF)
        {
            SBREntities db = new SBREntities();
            StringBuilder rtnSb = new StringBuilder();
            var attachList = db.TBL_SBR_ATTACH.Where(r => r.SBR_Id == SBR_Id & r.Attribute == EmitCnt).ToList();            

            foreach (var attach in attachList)
            {
                rtnSb.AppendFormat("File: {0} has deleted! \r\n", attach.FileOrigName);
                if (attach != null)
                {
                    var attr = db.TBL_SBR_ATTACH_ATTR.Where(r => r.HeaderId == attach.Id).ToList();
                    db.TBL_SBR_ATTACH_ATTR.RemoveRange(attr);
                    string filePath = attach.FileRealPath;
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            db.TBL_SBR_ATTACH.Remove(attach);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLine(ex.StackTrace);
                        return ex.StackTrace;
                    }
                }
            }
            try
            {
                if (db.SaveChanges() == 0)
                    return "Fail;" + rtnSb.ToString();
                else
                {
                    if (IsDelTF)
                    {
                        SBRController sbr = new SBRController();
                        int delTFRes = sbr.DeleteTestFlowBySBRIdNEmitCnt(SBR_Id, EmitCnt);
                        if (delTFRes == 0)
                            return "Fail; Delete test flow failed! ";
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                return ex.StackTrace;
            }

            return "Pass;" + rtnSb.ToString();
        }

        [HttpPost]
        [Route("DelFileBySBRId/{SBR_Id}")]
        public string DelFileBySBRId(Guid SBR_Id)
        {
            SBREntities db = new SBREntities();
            StringBuilder rtnSb = new StringBuilder();
            var attachList = db.TBL_SBR_ATTACH.Where(r => r.SBR_Id == SBR_Id).ToList();

            foreach (var attach in attachList)
            {
                rtnSb.AppendFormat("File: {0} has deleted! \r\n", attach.FileOrigName);
                if (attach != null)
                {
                    var attr = db.TBL_SBR_ATTACH_ATTR.Where(r => r.HeaderId == attach.Id).ToList();
                    db.TBL_SBR_ATTACH_ATTR.RemoveRange(attr);
                    string filePath = attach.FileRealPath;
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            db.TBL_SBR_ATTACH.Remove(attach);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLine(ex.StackTrace);
                        return ex.StackTrace;
                    }
                }
            }
            try
            {
                if (db.SaveChanges() == 0)
                    return "Fail;" + rtnSb.ToString();
            }
            catch (DbEntityValidationException ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                return ex.StackTrace;
            }

            return "Pass;" + rtnSb.ToString();
        }
    }
}
