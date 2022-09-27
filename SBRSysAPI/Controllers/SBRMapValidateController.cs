using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SBRSysAPI.Models.Classes;
using SBRSysAPI.Utils.MapValidator;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using System.Web.Http.Results;
using SystemLibrary.Utility;

namespace SBRSysAPI.Controllers
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class SBRMapValidateController : ApiController
    {
        // GET: api/SBRMapValidate
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/SBRMapValidate/5
        public string Get(int id)
        {
            string result = string.Empty;
            //string sbrHeaderId = "F6E0D5E3-1DB0-4366-923F-382CAC4E1646";
            //string emitCnt = "380";
            ////Emit Map
            //MapValidator mv = new MapValidator(sbrHeaderId, "F6E0D5E3-1DB0-4366-923F-382CAC4E1601", string.Empty);
            //string filePath = @"C:\Users\lic67888\Documents\00 Doc\01 All Product\SBR\Map Validator\WD048\WD048_OsakaGen2_MP_380e_EmitterMap_v0.csv";
            //result = mv.Validate(filePath, MapType.EmitMap);

            ////if (string.IsNullOrEmpty(result) == false) return result;

            ////TypeDef
            //mv = new MapValidator(sbrHeaderId, "F6E0D5E3-1DB0-4366-923F-382CAC4E1604", emitCnt);
            //filePath = @"C:\Users\lic67888\Documents\00 Doc\01 All Product\SBR\Map Validator\WD048\WD048_OsakaGen2_MP_TypeDef_v0.csv";
            //result = mv.Validate(filePath, MapType.TpDefMap);

            ////if (string.IsNullOrEmpty(result) == false) return result;

            //mv = new MapValidator(sbrHeaderId, "F6E0D5E3-1DB0-4366-923F-382CAC4E1602", emitCnt);
            //filePath = @"C:\Users\lic67888\Documents\00 Doc\01 All Product\SBR\Map Validator\WD048\WD048_OsakaGen2_MP_All_100%_TestMap_v0.csv";
            //result = mv.Validate(filePath, MapType.TestMap);

            //if (string.IsNullOrEmpty(result) == false) return result;

            //mv = new MapValidator(sbrHeaderId, "F6E0D5E3-1DB0-4366-923F-382CAC4E1603", emitCnt);
            //filePath = @"C:\Users\lic67888\Documents\00 Doc\01 All Product\SBR\Map Validator\WD048\WD048_OsakaGen2_MP_LIV_1%_TestMap_v0.csv";
            //result = mv.Validate(filePath, MapType.TestMap);
            return result;
        }

        [ResponseType(typeof(string))]
        [Route("api/SBRMapValidate")]
        [HttpPost]
        // POST: api/SBRMapValidate
        public string Post([FromBody]MapValidateClass data)
        {
            string result = string.Empty;
            try
            {
                MapValidateClass mapValItem = data;
                MapValidator mapValid = new MapValidator(mapValItem);
                result = mapValid.Validate(mapValItem.FilePath, mapValItem.MapType);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLine(ex.StackTrace);
                throw ex;
            }

            return result;
        }

        // PUT: api/SBRMapValidate/5
        public void Put(int id, [FromBody]string value)
        {

        }

        // DELETE: api/SBRMapValidate/5
        public void Delete(int id)
        {
        }
    }

}

