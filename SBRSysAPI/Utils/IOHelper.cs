using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Raven_LIV_Interpolate.Classes;
using SystemLibrary.Utility;
using Raven_LIV_Interpolate.Utils;
using System.Reflection;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Configuration;

namespace Raven_LIV_Interpolate.Utils
{
    public class IOHelper
    {
        /// <summary>
        /// Input File Path Then Return LIV Recal Format
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <returns></returns>
        public List<LIVRecal> LoadLIVFile(string filePath)
        {
            LogHelper.WriteLine("Retriving LIV file to list... Input file: " + filePath);
            List<LIVRecal> list = new List<LIVRecal>();
            try
            {
                List<Dictionary<string, string>> dic = ExcelOperator.GetCsvDataToDic(filePath);
                list = TranDicToList(dic);
                list.ForEach(r => r.Tested_dies = "1");
            }
            catch (Exception ex)
            {
                ExtensionHelper.LogAndSendMail(ex.ToString());
                throw ex;
            }
            return list;
        }

        private List<LIVRecal> TranDicToList(List<Dictionary<string, string>> dicList)
        {
            List<LIVRecal> list = new List<LIVRecal>();
            LIVRecal item = null;
            //Property and DisplayName Dictionary
            var livClsDic = TypeDescriptor.GetProperties(typeof(LIVRecal)).Cast<PropertyDescriptor>().ToDictionary(p => p.Name, p => p.DisplayName);
            foreach (Dictionary<string, string> dicItem in dicList)
            {
                item = new LIVRecal();
                foreach (var pi in item.GetType().GetProperties())
                {
                    if (livClsDic.ContainsKey(pi.Name) && dicItem.ContainsKey(livClsDic[pi.Name]))
                    {
                        var val = dicItem[livClsDic[pi.Name]];
                        pi.SetValue(item, string.IsNullOrEmpty(val) ? "0" : val.Trim(), null);
                    }
                }
                list.Add(item);
            }
            return list;
        }

        public List<TFSumLog> LoadTFSumFile(string filePath)
        {
            LogHelper.WriteLine("Retriving TF Summary file to list... Input file: " + filePath);
            List<TFSumLog> list = new List<TFSumLog>();
            try
            {
                List<Dictionary<string, string>> dic = ExcelOperator.GetCsvDataToDic(filePath);
                list = TranTFDicToList(dic);
            }
            catch (Exception ex)
            {
                ExtensionHelper.LogAndSendMail(ex.ToString());
                throw ex;
            }
            return list;
        }

        private List<TFSumLog> TranTFDicToList(List<Dictionary<string, string>> dicList)
        {
            List<TFSumLog> list = new List<TFSumLog>();
            TFSumLog item = null;
            //Property and DisplayName Dictionary
            var tfClsDic = TypeDescriptor.GetProperties(typeof(TFSumLog)).Cast<PropertyDescriptor>().ToDictionary(p => p.Name, p => p.DisplayName);
            foreach (Dictionary<string, string> dicItem in dicList)
            {
                item = new TFSumLog();
                foreach (var pi in item.GetType().GetProperties())
                {
                    if (tfClsDic.ContainsKey(pi.Name) && dicItem.ContainsKey(tfClsDic[pi.Name]))
                    {
                        var val = dicItem[tfClsDic[pi.Name]];
                        pi.SetValue(item, string.IsNullOrEmpty(val) ? "0" : val.Trim(), null);
                    }
                }
                list.Add(item);
            }
            return list;
        }


        public static void CreateCSVRawData(LIVRecal input, StringBuilder csvRaw)
        {
            char delimiter = ',';
            var sBColStr = ConfigurationManager.AppSettings["SumBlankColumns"];
            var sBColAry = sBColStr.Split(delimiter);
            if (csvRaw.Length == 0)
            {                
                foreach (PropertyInfo item in input.GetType().GetProperties())
                {
                    var dpItem = item.GetCustomAttributes(typeof(DisplayNameAttribute), true).FirstOrDefault() as DisplayNameAttribute;
                    csvRaw.AppendFormat("{0},", dpItem.DisplayName);
                }
                //Add blank column header
                if (string.IsNullOrEmpty(sBColStr) == false)
                    csvRaw.Append(sBColStr.Split(delimiter).Aggregate((i, j) => i + delimiter + j));
                csvRaw.Length--;
                csvRaw.AppendLine();
            }
            foreach (PropertyInfo item in input.GetType().GetProperties())
            {
                csvRaw.AppendFormat("{0},", item.GetValue(input, null));
            }
            //Add blank column body
            for (int i = 0; i < sBColAry.Count(); i++)
            {
                csvRaw.Append(",");
            }
            csvRaw.Length--;
            csvRaw.AppendLine();
        }

        public static string CheckFilePathAndArchiveData(string outputPath)
        {
            return CheckFilePathAndArchiveData(outputPath, null);
        }

        /// <summary>
        /// Check File Path Exsits and Archive Old data
        /// </summary>
        /// <param name="outputPath"></param>
        /// <param name="newFileName"></param>
        /// <returns></returns>
        public static string CheckFilePathAndArchiveData(string outputPath, List<string> moveFileList, bool isMoveAll)
        {
            string archivePath = string.Format(@"{0}\Archive", outputPath);
            if (Directory.Exists(outputPath) == false)
                Directory.CreateDirectory(outputPath);                
            if (Directory.Exists(archivePath) == false)
                Directory.CreateDirectory(archivePath);
            var dirFiles = Directory.GetFiles(outputPath);
            FileInfo fi = null;
            if (dirFiles.Count() > 0)
            {
                foreach (string fileName in dirFiles)
                {
                    fi = new FileInfo(fileName);
                    if (isMoveAll)
                    {
                        FileHelper.Move(fileName, archivePath, new FileInfo(fileName).Name + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    }
                    else
                    {
                        if (CheckFileExist(moveFileList, fi.Name))
                        {
                            FileHelper.Move(fileName, archivePath, new FileInfo(fileName).Name + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                        }
                    }                    
                }
            }
            return outputPath;
        }

        public static string CheckFilePathAndArchiveData(string outputPath, List<string> moveFileList)
        {
            return CheckFilePathAndArchiveData(outputPath, moveFileList, false);
        }

        private static bool CheckFileExist(List<string> moveFileList, string checkFilePath)
        {
            string checkFileName = Path.GetFileNameWithoutExtension(checkFilePath);
            if (moveFileList != null)
            {                
                if (moveFileList.Find(r => Path.GetFileNameWithoutExtension(new FileInfo(r).Name).Contains(checkFileName)) != null)
                {
                    return true;
                }
            }
            else
            {
                if (checkFileName.StartsWith(string.Format("{0}_{1}", Program._WaferID, Program._NF_MAP_OP)))
                {
                    return true;
                }
            }

            return false;
        }
        //public static string FileNameDateTimeParser(string input)
        //{
        //    DateTime dt;
        //    string test = "test_1234_TTEE_20180128_0523";
        //    var regex = new Regex(@"\d{2,2}\d{2,2}\d{4,4}_\d{2,2}\d{2,2}");
        //    foreach (Match m in regex.Matches(test))
        //    {
                
        //        if (DateTime.TryParseExact(m.Value, "yyyyMMdd_HHmm", null, DateTimeStyles.None, out dt))
        //            Console.WriteLine(dt.ToString());
        //    }

        //    return dt.ToString("yyyyMMdd_HHmm");
        //}
    }
}
