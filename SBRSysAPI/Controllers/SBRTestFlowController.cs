using SBRSysAPI.Models;
using SBRSysAPI.Utils;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Web.Http;
using System.Web.Http.Cors;
using SystemLibrary.Utility;

namespace SBRSysAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/SBRTestFlow")]
    public class SBRTestFlowController : ApiController
    {

        [HttpGet]
        [Route("GetTestFlowLookup")]
        public HttpResponseMessage GetTestFlowLookup(string type)
        {
            SBREntities db = new SBREntities();
            string version = "1";
            var resp = new HttpResponseMessage(HttpStatusCode.OK);

            try
            {
                var typeLatestVer = db.TBL_SBR_Dynamic_ColumnLookup.Where(r => r.DCType == type && r.IsShow == "Y").OrderByDescending(r => r.Version);

                if (typeLatestVer != null && typeLatestVer.Count() > 0)
                    version = typeLatestVer.FirstOrDefault().Version;

                var list = db.TBL_SBR_Dynamic_ColumnLookup.Where(r => r.Version == version && r.DCType == type)
                    .OrderBy(r => r.LabelSequence).OrderBy(r => r.GroupSequence).OrderBy(r => r.DCType);
                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        [HttpGet]
        [Route("GetTestFlowTypeDefGroup/{testFlowId}")]
        public HttpResponseMessage GetTestFlowTypeDefGroup(string testFlowId)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                var testFlowGuid = Guid.Parse(testFlowId);
                StringBuilder sb = new StringBuilder();
                var tpDefFilePath = (from tfItem in db.TBL_SBR_TestFlow
                                     join attachItem in db.TBL_SBR_ATTACH on tfItem.TpDefMap equals attachItem.FileOrigName
                                     where tfItem.Id == testFlowGuid
                                     select attachItem.FileRealPath).FirstOrDefault();
                if (string.IsNullOrEmpty(tpDefFilePath) == false && File.Exists(tpDefFilePath))
                {
                    List<Dictionary<string, string>> listDic = ExcelOperator.GetCsvDataToDicReverse(tpDefFilePath);
                    foreach (Dictionary<string, string> dic in listDic)
                    {
                        if (sb.ToString().Contains(dic["Group"].ToString()))
                            continue;
                        sb.AppendFormat("{0},", dic["Group"].ToString());
                    }
                    if (sb.Length > 0) sb.Length--;
                    var rtnObj = new
                    {
                        Groups = sb.ToString()
                    };
                    resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
                }
                else
                {
                    resp = IOUtils.LogAndResponse(null, HttpStatusCode.NoContent, "Missing TypeDef File Path!");
                }
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        #region CRUD for TBL_SBR_Dynamic_ColumnResult
        [HttpGet]
        [Route("GetTBL_SBR_Dynamic_ColumnResult/{testFlowId}")]
        public HttpResponseMessage GetTBL_SBR_Dynamic_ColumnResult(string testFlowId, string headerId = "")
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);

            try
            {
                var testFlowGuid = Guid.Parse(testFlowId);
                var list = db.TBL_SBR_Dynamic_ColumnResult.Where(r => r.TestFlowId == testFlowGuid).
                    OrderBy(r => r.Creation_Date).ToList();
                if (list != null && list.Count() > 0 && string.IsNullOrEmpty(headerId) == false)
                {
                    var headerGuid = Guid.Parse(headerId);
                    list = list.Where(r => r.Id == headerGuid).ToList();
                }

                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        [HttpPost]
        [Route("PostTBL_SBR_Dynamic_ColumnResult")]
        public HttpResponseMessage PostTBL_SBR_Dynamic_ColumnResult([FromBody] List<TBL_SBR_Dynamic_ColumnResult> list)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int res = 0;
            try
            {
                var idList = list.Select(r => r.Id.ToString()).ToList();
                var exsitList = db.TBL_SBR_Dynamic_ColumnResult.Where(r => idList.Contains(r.Id.ToString()));
                var exsitItem = new TBL_SBR_Dynamic_ColumnResult();
                foreach (var inputItem in list)
                {
                    exsitItem = exsitList.Where(r => r.Id == inputItem.Id).FirstOrDefault();
                    if (exsitItem != null)
                    {
                        ReflectionHelper.CopyProperties(inputItem, exsitItem);
                        exsitItem.Last_Updated_Date = DateTime.Now;
                        db.Entry(exsitItem).State = EntityState.Modified;
                    }
                    else
                    {
                        inputItem.Creation_Date = DateTime.Now;
                        inputItem.Last_Updated_Date = DateTime.Now;
                        inputItem.Id = Guid.NewGuid();
                        db.TBL_SBR_Dynamic_ColumnResult.Add(inputItem);
                    }
                }

                res = db.SaveChanges();
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        [HttpPost]
        [Route("DelTBL_SBR_Dynamic_ColumnResult/{testFlowId}")]
        public HttpResponseMessage DelTBL_SBR_Dynamic_ColumnResult(string testFlowId)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                string sql = string.Format("Delete from TBL_SBR_Dynamic_ColumnResult where TestFlowId = '{0}' ", testFlowId);
                var res = db.Database.ExecuteSqlCommand(sql);
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }
        #endregion

        /*
        #region CRUD for TBL_SBR_TestFlow_LIV
        /// <summary>
        /// Get TestFlowLIV Info
        /// </summary>
        /// <param name="sbrRDId"></param>
        /// <param name="testFlowId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("TBL_SBR_TestFlow_LIV/{sbrRDId}")]
        public HttpResponseMessage GetTBL_SBR_TestFlow_LIV(string sbrRDId, string testFlowId = "")
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                List<TBL_SBR_TestFlow_LIV> list = new List<TBL_SBR_TestFlow_LIV>();
                var sbrRDGuid = Guid.Parse(sbrRDId);
                if (string.IsNullOrEmpty(testFlowId))
                {
                    list = db.TBL_SBR_TestFlow_LIV.Where(r => r.SBR_RD_Header_Id == sbrRDGuid).ToList();
                }
                else
                {
                    var testFlowGuid = Guid.Parse(testFlowId);
                    list = db.TBL_SBR_TestFlow_LIV.Where(r => r.SBR_RD_Header_Id == sbrRDGuid
                    && r.Testflow_Id == testFlowGuid).ToList();
                }

                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        /// <summary>
        /// Insert Update TestFlowLIV
        /// </summary>
        /// <param name="inputItem"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("PostTBL_SBR_TestFlow_LIV")]
        public HttpResponseMessage PostTBL_SBR_TestFlow_LIV([FromBody] TBL_SBR_TestFlow_LIV inputItem)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);

            int res = 0;
            try
            {
                var exsitItem = db.TBL_SBR_TestFlow_LIV.Where(r => r.Id == inputItem.Id).FirstOrDefault();
                if (exsitItem != null)
                {
                    ReflectionHelper.CopyProperties(inputItem, exsitItem);
                    exsitItem.Last_Updated_Date = DateTime.Now;
                    db.Entry(exsitItem).State = EntityState.Modified;
                }
                else
                {
                    inputItem.Id = Guid.NewGuid();
                    inputItem.Creation_Date = DateTime.Now;
                    inputItem.Last_Updated_Date = DateTime.Now;
                    db.TBL_SBR_TestFlow_LIV.Add(inputItem);
                }

                res = db.SaveChanges();
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        /// <summary>
        /// Delete SBR_TestFlow_LIV
        /// </summary>
        /// <param name="testflow_Id"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("DelTBL_SBR_TestFlow_LIV/{testflow_Id}")]
        public HttpResponseMessage DelTBL_SBR_TestFlow_LIV(string testflow_Id)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                string sql = string.Format("Delete from TBL_SBR_TestFlow_LIV where Testflow_Id = '{0}' ", testflow_Id);
                var res = db.Database.ExecuteSqlCommand(sql);
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }
        #endregion

        #region CRUD for TBL_SBR_TestFlow_Criterias
        [HttpGet]
        [Route("GetTBL_SBR_TestFlow_Criterias/{testFlowId}")]
        public HttpResponseMessage GetTBL_SBR_TestFlow_Criterias(string testFlowId)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                var testFlowGuid = Guid.Parse(testFlowId);
                var list = db.TBL_SBR_TestFlow_Criterias.Where(r => r.Testflow_Id == testFlowGuid).
                    OrderBy(r => r.Sequence).ToList();

                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        /// <summary>
        /// Insert Update TBL_SBR_TestFlow_Criterias by List
        /// </summary>
        /// <param name="list">List<TBL_SBR_TestFlow_Criterias> list</param>
        /// <returns></returns>
        [HttpPost]
        [Route("PostTBL_SBR_TestFlow_Criterias")]
        public HttpResponseMessage PostTBL_SBR_TestFlow_Criterias([FromBody] List<TBL_SBR_TestFlow_Criterias> list)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int res = 0;
            try
            {
                var idList = list.Select(r => r.Id.ToString()).ToList();
                var exsitList = db.TBL_SBR_TestFlow_Criterias.Where(r => idList.Contains(r.Id.ToString()));
                var exsitItem = new TBL_SBR_TestFlow_Criterias();
                foreach (var inputItem in list)
                {
                    exsitItem = exsitList.Where(r => r.Id == inputItem.Id).FirstOrDefault();
                    if (exsitItem != null)
                    {
                        ReflectionHelper.CopyProperties(inputItem, exsitItem);
                        exsitItem.Last_Updated_Date = DateTime.Now;
                        db.Entry(exsitItem).State = EntityState.Modified;
                    }
                    else
                    {
                        inputItem.Creation_Date = DateTime.Now;
                        inputItem.Last_Updated_Date = DateTime.Now;
                        inputItem.Id = Guid.NewGuid();
                        db.TBL_SBR_TestFlow_Criterias.Add(inputItem);
                    }
                }

                res = db.SaveChanges();
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        /// <summary>
        /// Delete TBL_SBR_TestFlow_Criterias
        /// </summary>
        /// <param name="testFlowId">TestFlow Id</param>
        /// <param name="headerId">TBL_SBR_TestFlow_Criterias Unique Id</param>
        /// <returns></returns>
        [HttpPost]
        [Route("DelTBL_SBR_TestFlow_Criterias/{testFlowId}")]
        public HttpResponseMessage DelTBL_SBR_TestFlow_Criterias(string testFlowId, string headerId = "")
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                string sql = string.Format("Delete from TBL_SBR_TestFlow_Criterias where TestFlow_Id = '{0}' {1} ", testFlowId,
                                        string.IsNullOrEmpty(headerId) ? "" : string.Format("and Id = {0}", headerId));

                var res = db.Database.ExecuteSqlCommand(sql);
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }
        #endregion

        #region CRUD for TBL_SBR_TestFlow_Parameters
        /// <summary>
        /// Get TBL_SBR_TestFlow_Parameters 
        /// </summary>
        /// <param name="testFlowId">Test Flow Id</param>
        /// <param name="parameterType">Parameter Type</param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetTBL_SBR_TestFlow_Parameters/{testFlowId}")]
        public HttpResponseMessage GetTBL_SBR_TestFlow_Parameters(string testFlowId, string parameterType = "")
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                var testFlowGuid = Guid.Parse(testFlowId);
                var list = db.TBL_SBR_TestFlow_Parameters.Where(r => r.Testflow_Id == testFlowGuid).
                    OrderBy(r => r.Index).OrderBy(r => r.ParameterType).ToList();

                if (string.IsNullOrEmpty(parameterType) == false)
                {
                    list = list.Where(r => r.ParameterType == parameterType).OrderBy(r => r.Index).ToList();
                }

                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        /// <summary>
        /// Insert Update TBL_SBR_TestFlow_Parameters
        /// </summary>
        /// <param name="list">List<TBL_SBR_TestFlow_Parameters> list</param>
        /// <returns></returns>
        [HttpPost]
        [Route("PostTBL_SBR_TestFlow_Parameters")]
        public HttpResponseMessage PostTBL_SBR_TestFlow_Parameters([FromBody] List<TBL_SBR_TestFlow_Parameters> list)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int res = 0;
            try
            {
                var idList = list.Select(r => r.Id.ToString()).ToList();
                var exsitList = db.TBL_SBR_TestFlow_Parameters.Where(r => idList.Contains(r.Id.ToString()));
                var exsitItem = new TBL_SBR_TestFlow_Parameters();
                foreach (var inputItem in list)
                {
                    exsitItem = exsitList.Where(r => r.Id == inputItem.Id).FirstOrDefault();
                    if (exsitItem != null)
                    {
                        ReflectionHelper.CopyProperties(inputItem, exsitItem);
                        exsitItem.Last_Updated_Date = DateTime.Now;
                        db.Entry(exsitItem).State = EntityState.Modified;
                    }
                    else
                    {
                        inputItem.Creation_Date = DateTime.Now;
                        inputItem.Last_Updated_Date = DateTime.Now;
                        inputItem.Id = Guid.NewGuid();
                        db.TBL_SBR_TestFlow_Parameters.Add(inputItem);
                    }
                }

                res = db.SaveChanges();
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        /// <summary>
        /// Delete TBL_SBR_TestFlow_Parameters
        /// </summary>
        /// <param name="testFlowId">TestFlow Id</param>
        /// <param name="headerId">TBL_SBR_TestFlow_Parameters Unique Id</param>
        /// <returns></returns>
        [HttpPost]
        [Route("DelTBL_SBR_TestFlow_Parameters/{testFlowId}")]
        public HttpResponseMessage DelTBL_SBR_TestFlow_Parameters(string testFlowId, string parameterType = "", string headerId = "")
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                string sql = string.Format("Delete from TBL_SBR_TestFlow_Parameters where TestFlow_Id = '{0}' {1} {2} ", testFlowId,
                                        string.IsNullOrEmpty(headerId) ? "" : string.Format("and Id = '{0}'", headerId),
                                        string.IsNullOrEmpty(parameterType) ? "" : string.Format("and ParameterType = '{0}'", parameterType));

                var res = db.Database.ExecuteSqlCommand(sql);
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }
        #endregion
        */

        #region CRUD for TBL_SBR_TestFlow_Steps
        /// <summary>
        /// Get TBL_SBR_TestFlow_Steps
        /// </summary>
        /// <param name="testFlowId">TestFlow Id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetTBL_SBR_TestFlow_Steps/{testFlowId}")]
        public HttpResponseMessage GetTBL_SBR_TestFlow_Steps(string testFlowId)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            try
            {
                var testFlowGuid = Guid.Parse(testFlowId);
                var list = db.TBL_SBR_TestFlow_Steps.Where(r => r.Testflow_Id == testFlowGuid).
                    OrderBy(r => r.Step).ToList();

                var rtnObj = new
                {
                    OutputList = list.ToList(),
                    TotalCount = list.Count()
                };
                resp = SBROperHelper.LogAndResponse(new ObjectContent<dynamic>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        /// <summary>
        /// Insert Update TBL_SBR_TestFlow_Steps
        /// </summary>
        /// <param name="list">List<TBL_SBR_TestFlow_Steps> list</param>
        /// <returns></returns>
        [HttpPost]
        [Route("PostTBL_SBR_TestFlow_Steps")]
        public HttpResponseMessage PostTBL_SBR_TestFlow_Steps([FromBody] List<TBL_SBR_TestFlow_Steps> list)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int res = 0;
            try
            {
                var idList = list.Select(r => r.Id.ToString()).ToList();
                var exsitList = db.TBL_SBR_TestFlow_Steps.Where(r => idList.Contains(r.Id.ToString()));
                var exsitItem = new TBL_SBR_TestFlow_Steps();
                foreach (var inputItem in list)
                {
                    exsitItem = exsitList.Where(r => r.Id == inputItem.Id).FirstOrDefault();
                    if (exsitItem != null)
                    {
                        ReflectionHelper.CopyProperties(inputItem, exsitItem);
                        exsitItem.Last_Updated_Date = DateTime.Now;
                        db.Entry(exsitItem).State = EntityState.Modified;
                    }
                    else
                    {
                        inputItem.Creation_Date = DateTime.Now;
                        inputItem.Last_Updated_Date = DateTime.Now;
                        inputItem.Id = Guid.NewGuid();
                        db.TBL_SBR_TestFlow_Steps.Add(inputItem);
                    }
                }

                res = db.SaveChanges();
                var rtnObj = new { TxnCount = res };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }

        /// <summary>
        /// Delete TBL_SBR_TestFlow_Steps
        /// </summary>
        /// <param name="testFlowId">TestFlow Id</param>
        /// <param name="headerId">TBL_SBR_TestFlow_Steps Unique Id</param>
        /// <returns></returns>
        [HttpPost]
        [Route("DelTBL_SBR_TestFlow_Steps/{testFlowId}")]
        public HttpResponseMessage DelTBL_SBR_TestFlow_Steps(string testFlowId, string headerId = "")
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            SBREntities db = new SBREntities();
            try
            {
                string sql = string.Format("Delete from TBL_SBR_TestFlow_Steps where TestFlow_Id = '{0}' {1} ", testFlowId,
                    string.IsNullOrEmpty(headerId) ? "" : string.Format("and Id = {0}", headerId));

                var res = db.Database.ExecuteSqlCommand(sql);
                var rtnObj = new
                {
                    TxnCount = res
                };
                resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }

            return resp;
        }
        #endregion
    }
}