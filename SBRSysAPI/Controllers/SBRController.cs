using SBRSysAPI.Models;
using SBRSysAPI.Models.Classes;
using SBRSysAPI.Utils;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
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
    [RoutePrefix("api/SBR")]
    public class SBRController : ApiController
    {
        // GET: api/SBR
        public List<TBL_SBR_RD_Header> Get()
        {
            SBREntities db = new SBREntities();
            List<int> showStatus = new List<int>() { 1, 2, 3, 4 };
            //0 Disable, 1 In-Process, 2 Waiting Approval, 3 Approved, 4 Amended, 99 Reject for Temp
            var list = db.TBL_SBR_RD_Header.Where(r => showStatus.Contains(r.Status.Value)).OrderByDescending(r=>r.SBR_Num).ThenByDescending(r=> r.Version).ToList();

            foreach (var item in list)
            {
                item.Created_By = SBROperHelper.GetUserNameByAcc(item.Created_By);
            }

            return list;
        }

        // GET: api/SBR/GetSBRNum
        [HttpGet]
        [Route("GetSBRNum")]
        public TBL_SBR_RD_Header GetSBRNum()
        {
            TBL_SBR_RD_Header sbrHeader = new TBL_SBR_RD_Header();
            List<int> usedStatus = new List<int>() { 1, 2, 3, 4 };
            DateTime nowDt = DateTime.Today.Date;
            SBREntities db = new SBREntities();
            sbrHeader.SBR_Num = "SBR-" + nowDt.ToString("yyyyMMdd");
            //sbrHeader.SBR_Num = "SBR-20191029"; //Test Only
            string sbrNumDate = string.Empty;
            string sbrSeq = string.Empty;
            var latestSBRNum = (from header in db.TBL_SBR_RD_Header
                                    //where DbFunctions.TruncateTime(header.Creation_Date) == DbFunctions.TruncateTime(nowDt) 
                                where header.SBR_Num.Contains(sbrHeader.SBR_Num) //ex: SBR-yyyyMMdd (Prefix)
                                && usedStatus.Contains(header.Status.Value)
                                select header).OrderByDescending(r => r.SBR_Num).FirstOrDefault();
            if (latestSBRNum != null)
            {
                var latSBRNumAry = latestSBRNum.SBR_Num.Split('-');
                if (latSBRNumAry.Count() == 3)
                {
                    var todaySeq = int.Parse(latSBRNumAry[2].ToString());
                    var newSeq = todaySeq + 1;
                    sbrNumDate = latSBRNumAry[1];
                    sbrSeq = newSeq.ToString("-00");
                    sbrHeader.SBR_Num = "SBR-" + sbrNumDate + sbrSeq;
                }
                else
                {
                    sbrHeader.SBR_Num = sbrHeader.SBR_Num + "-01";
                }
            }
            else
            {
                sbrHeader.SBR_Num = sbrHeader.SBR_Num + "-01";
            }
            sbrHeader.Id = Guid.NewGuid();
            return sbrHeader;
        }

        // GET: api/SBR/5
        public TBL_SBR_RD_Header Get(Guid id)
        {
            SBREntities db = new SBREntities();
            var existHeader = db.TBL_SBR_RD_Header.Where(r => r.Id == id).FirstOrDefault();
            if (existHeader != null && existHeader.CostAmt != null)
            {
                existHeader.CostAmt = decimal.Parse(Math.Round(double.Parse(existHeader.CostAmt.ToString()), 2).ToString());
            }
            return existHeader;
        }

        // GET: api/SBR/GetRDApprovers
        [HttpGet]
        [Route("GetRDApprovers")]
        public List<string> GetRDApprovers()
        {
            SBREntities db = new SBREntities();
            List<string> apprTypeList = new List<string>() { "Approver", "MP_Approver" };
            var rdApprList = db.TBL_SBR_Lookup.Where(r => apprTypeList.Contains(r.LkType)
            && r.LkKey == "RD" && r.Status == 1).Select(r => r.Attribute).Distinct().ToList();
            return rdApprList;
        }

        // POST: api/SBR
        public void Post([FromBody]string value)
        {
        }

        //0 disable, 1 processing, 2 in-approval, 3 approved, 4 Amended
        /// <summary>
        /// Post RD Header
        /// </summary>
        /// <param name="header">Status -> 0 Disable, 1 Processing, 2 In-Approval, 3 Approved, 4 Amended</param>
        /// <returns></returns>
        [HttpPost]
        [Route("PostRDHeader")]
        public int PostRDHeader([FromBody] TBL_SBR_RD_Header header)
        {
            SBREntities db = new SBREntities();
            int res = 0;
            try
            {
                var existHeader = db.TBL_SBR_RD_Header.Where(r => r.Id == header.Id).FirstOrDefault();
                var calHeader = SBROperHelper.CalculateCostAmt(header.Id, 1, 1, 1, header.LotType);
                if (existHeader == null)
                {
                    header.Creation_Date = DateTime.Now;
                    header.Last_Updated_Date = DateTime.Now;
                    if (calHeader != null)
                        header.CostAmt = calHeader.CostAmt;
                    db.TBL_SBR_RD_Header.Add(header);
                }
                else
                {
                    ReflectionHelper.CopyProperties(header, existHeader);
                    existHeader.Last_Updated_Date = DateTime.Now;
                    //existHeader.ProductName = header.ProductName;
                    //existHeader.Pulse_Width = header.Pulse_Width;
                    //existHeader.Duty_Cycle = header.Duty_Cycle;
                    //existHeader.Wafer_ID = header.Wafer_ID;
                    //existHeader.LastUpdated_By = header.LastUpdated_By;
                    //existHeader.Version = header.Version;
                    //existHeader.Requestor = header.Requestor;
                    //existHeader.ApprComment = header.ApprComment;
                    //existHeader.LotType = header.LotType;
                    if (calHeader != null)
                        existHeader.CostAmt = calHeader.CostAmt;
                    if (header.Status == 99) //Reject then update to In-Process
                        existHeader.Status = 1;
                    else
                        existHeader.Status = header.Status;                    
                    db.Entry(existHeader);
                }
                
                res = db.SaveChanges();
                if (res > 0)
                {
                    SBROperHelper.SBRApprovalTransaction(header);
                }
            }
            catch (Exception e)
            {
                string exStr = string.Format("StackTrace: {0} \n\r Inner Exception: {1}", e.StackTrace, e.InnerException);
                LogHelper.WriteLine(exStr);
                throw e;
            }

            return res;
        }

        [HttpPost]
        [Route("PostTestFlow")]
        //Save and Update TestFlow
        public HttpResponseMessage PostTestFlow([FromBody] TBL_SBR_TestFlow testFlow)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int tfSuccessCnt = 0; //Test flow change count
            int hdSuccessCnt = 0; //Header change count
            try
            {
                var existTF = db.TBL_SBR_TestFlow.Where(r => r.Id == testFlow.Id).FirstOrDefault();
                testFlow.TestTime = SBROperHelper.GetCostTime(testFlow); //Calculate CostTime
                if (existTF == null) //New Item Coming
                {
                    testFlow.Id = Guid.NewGuid();                    
                    testFlow.Creation_Date = DateTime.Now;
                    testFlow.Last_Updated_Date = DateTime.Now;
                    testFlow.Status = 1;
                    db.TBL_SBR_TestFlow.Add(testFlow);
                }
                else
                {
                    var props = new TBL_SBR_TestFlow().GetType().GetProperties();
                    foreach (var prop in props)
                    {
                        prop.SetValue(existTF, prop.GetValue(testFlow, null), null);
                    }
                    existTF.Last_Updated_Date = DateTime.Now;
                    db.Entry(existTF);
                }

                tfSuccessCnt = db.SaveChanges();
                if (tfSuccessCnt > 0)
                {                    
                    var header = SBROperHelper.CalculateCostAmt(testFlow.SBR_RD_Header_Id);
                    header.LastUpdated_By = testFlow.LastUpdated_By;
                    db.Entry(header).State = EntityState.Modified;
                    hdSuccessCnt = db.SaveChanges();
                    var rtnObj = new
                    {
                        Head_CostAmt = Math.Round(double.Parse(header.CostAmt.ToString()), 2),
                        Head_SucsCnt = hdSuccessCnt,
                        TF_TestTime = Math.Round(double.Parse(testFlow.TestTime), 3),
                        TF_SucCnt = tfSuccessCnt,
                        TestFlowId = testFlow.Id
                    };
                    resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
                }
                else {
                    resp = IOUtils.LogAndResponse(null, HttpStatusCode.NotAcceptable, "DB Save Failed!");
                }
            }
            catch (DbEntityValidationException e)
            {
                var entityError = e.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                var getFullMessage = string.Join("; ", entityError);
                var exceptionMessage = string.Concat(e.Message, "errors are: ", getFullMessage);

                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", exceptionMessage, e.StackTrace));
            }
            catch (DbUpdateException e)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", e.Message, e.StackTrace));
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        [HttpGet]
        [Route("GetTestFlowBySBRId/{guid}")]
        //Query Test Flow By SBR ID
        public List<TBL_SBR_TestFlow> GetTestFlowBySBRId(Guid guid)
        {
            SBREntities db = new SBREntities();
            try
            {
                var list = db.TBL_SBR_TestFlow.Where(r => r.SBR_RD_Header_Id == guid && r.Status == 1).ToList();
                if (list.Count() > 0)
                {
                    foreach (var item in list)
                    {
                        if (string.IsNullOrEmpty(item.TestTime) == false)
                            item.TestTime = Math.Round(double.Parse(item.TestTime), 3).ToString();
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.InnerException.Message);
                return null;
            }
        }

        //Delete Test Flow By ID
        [HttpPost]
        [Route("DeleteTestFlowById/{TFID}/{HEADERID}")]
        public HttpResponseMessage DeleteTestFlowById(Guid tfID, Guid headerID)
        {
            SBREntities db = new SBREntities();
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            int tfSuccessCnt = 0; //Test flow change count
            int hdSuccessCnt = 0; //Header change count
            try
            {
                var list = db.TBL_SBR_TestFlow.Where(r => r.Id == tfID && r.Status == 1).ToList();
                foreach (var item in list)
                {
                    item.Status = 0;
                    db.Entry(item);
                }
                tfSuccessCnt = db.SaveChanges();
                if (tfSuccessCnt > 0)
                {
                    var header = SBROperHelper.CalculateCostAmt(headerID);
                    header.LastUpdated_By = "";
                    db.Entry(header).State = EntityState.Modified;
                    hdSuccessCnt = db.SaveChanges();
                    var rtnObj = new
                    {
                        Head_CostAmt = Math.Round(double.Parse(header.CostAmt.ToString()), 2),
                        Head_SucsCnt = hdSuccessCnt
                    };
                    resp = IOUtils.LogAndResponse(new ObjectContent<object>(rtnObj, new JsonMediaTypeFormatter()));
                }
                else
                {
                    resp = IOUtils.LogAndResponse(null, HttpStatusCode.NotAcceptable, "DB Deleted Failed!");
                }
            }
            catch (Exception ex)
            {
                resp = IOUtils.LogAndResponse(null, HttpStatusCode.ExpectationFailed, string.Format(@"Message: {0},
                StackTrace: {1} ", ex.Message, ex.StackTrace));
            }
            return resp;
        }

        //Delete Test Flow By SBR ID
        [HttpPost]
        [Route("DeleteTestFlowBySBRId/{guid}")]
        public int DeleteTestFlowBySBRId(Guid guid)
        {
            SBREntities db = new SBREntities();
            try
            {
                var list = db.TBL_SBR_TestFlow.Where(r => r.SBR_RD_Header_Id == guid && r.Status == 1).ToList();
                foreach (var item in list)
                {
                    item.Status = 0;
                    db.Entry(item);
                }
                return db.SaveChanges();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.InnerException.Message);
                return 0;
            }
        }

        //Delete Test Flow By SBR ID And Emitter Count
        [HttpPost]
        [Route("DeleteTestFlowBySBRIdNEmitCnt/{guid}/{emitCount}")]
        public int DeleteTestFlowBySBRIdNEmitCnt(Guid guid, string emitCount)
        {
            SBREntities db = new SBREntities();
            try
            {
                var list = db.TBL_SBR_TestFlow.Where(r => r.SBR_RD_Header_Id == guid 
                && r.EmitCount == emitCount && r.Status == 1).ToList();
                foreach (var item in list)
                {
                    item.Status = 0;
                    db.Entry(item);
                }
                return db.SaveChanges();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.InnerException.Message);
                return 0;
            }
        }

        // PUT: api/SBR/5
        public void Put(int id, [FromBody]string value)
        {
        }

        [HttpPost]
        [Route("RemoveRDHeader/{guid}")]
        public int RemoveRDHeader(Guid guid)
        {
            SBREntities db = new SBREntities();
            var existHeader = db.TBL_SBR_RD_Header.Where(r => r.Id == guid).FirstOrDefault();
            if (existHeader != null)
            {
                if (existHeader.Version > 1)
                {
                    var latestVer = existHeader.Version - 1;
                    var latestHeader = db.TBL_SBR_RD_Header.Where(r => r.SBR_Num == existHeader.SBR_Num 
                    && r.Version == latestVer && r.Status == 4).FirstOrDefault();
                    if (latestHeader != null)
                    {
                        latestHeader.Status = 3;
                        db.Entry(latestHeader);
                    }
                }
                existHeader.Status = 0;
                db.Entry(existHeader);
            }
            return db.SaveChanges();
        }

        /// <summary>
        /// Query SBR Info By SBR Number
        /// </summary>
        /// <param name="sbrNumber">SBR Number</param>
        /// <returns></returns>
        [HttpGet]
        [Route("GetSBRBySBRNumber/{sbrNumber}")]
        public HttpResponseMessage GetSBRBySBRNumber(string sbrNumber)
        {
            HttpResponseMessage resp = new HttpResponseMessage(HttpStatusCode.OK);
            SBREntities db = new SBREntities();
            List<TBL_SBR_TFDIES> focusDieList = new List<TBL_SBR_TFDIES>();
            List<TBL_SBR_DYNAMIC_RESLT> dynamicRsList = new List<TBL_SBR_DYNAMIC_RESLT>();
            Dictionary<int, List<TBL_SBR_TestFlow_Steps>> testFlowStepList = new Dictionary<int, List<TBL_SBR_TestFlow_Steps>>();
            //List<TBL_SBR_TestFlow_Steps> testFlowStepList = new List<TBL_SBR_TestFlow_Steps>();
            try
            {                
                var existHeader = db.TBL_SBR_RD_Header.Where(r => r.SBR_Num == sbrNumber).FirstOrDefault();
                if (existHeader != null && existHeader.CostAmt != null)
                {
                    existHeader.CostAmt = decimal.Parse(Math.Round(double.Parse(existHeader.CostAmt.ToString()), 2).ToString());

                    var list = db.TBL_SBR_TestFlow.Where(r => r.SBR_RD_Header_Id == existHeader.Id && r.Status == 1).ToList();
                    if (list.Count() > 0)
                    {
                        int nonAfDieCnt;
                        int aFDieCnt;
                        foreach (var item in list)
                        {
                            nonAfDieCnt = 0;
                            aFDieCnt = 0;
                            if (string.IsNullOrEmpty(item.TestTime) == false)
                                item.TestTime = Math.Round(double.Parse(item.TestTime), 3).ToString();
                            SBROperHelper.GetFocusDiesFromTestFlow(item, ref nonAfDieCnt, ref aFDieCnt);
                            focusDieList.Add(new TBL_SBR_TFDIES()
                            {
                                Id = item.Id,
                                Seq = item.Seq,
                                AutoFocusDie = aFDieCnt,
                                NonAutoFocusDie = nonAfDieCnt
                            });

                            dynamicRsList.AddRange(SBROperHelper.GetDynamicResultByTestFlowId(item));
                            testFlowStepList.Add(item.Seq, SBROperHelper.GetTestFlowStepsByTestFlowId(item));
                        }
                    }

                    var returnObj = new { SBRHeader = existHeader, SBRTestFlowList = list.OrderBy(r => r.Seq), SBRTestFlowFocusDieList = focusDieList.OrderBy(r => r.Seq), SBRTestFlowDynamicResultList = dynamicRsList.OrderBy(r => r.Seq), TestFlowSteps = testFlowStepList.OrderBy(r => r.Key) };
                    resp.Content = new ObjectContent<object>(returnObj, new JsonMediaTypeFormatter());
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.InnerException.Message);
                resp.Content = new StringContent(ex.Message, Encoding.UTF8, "text/html");
            }

            return resp;
        }

    }
}
