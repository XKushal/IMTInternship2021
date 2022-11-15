using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.ServiceModel.Channels;
using WinSCP;
using System.Web;
using System.Net;
using System.Configuration;

namespace SpectrumServices
{
    class ImtCallHome
    {
        DB db = new DB();
        ErrorLog err = new ErrorLog();
        string vbCRLF = System.Environment.NewLine;

        public void GetImtCallHomeData()
        {
            List<String> allCarriers = new List<String>();
            string settingName = "imtCallHome";
            string settingValue = "";
            allCarriers = db.ReportAllCarriers();
            foreach (var sCarrier in allCarriers)
            {
                settingValue = db.CheckMasterConfig(sCarrier, settingName).ToUpper();
                if (!settingValue.Equals("") && settingValue.Equals("ON"))
                {
                    PostJsonResult(sCarrier);
                }
            }
        }

        public void PostJsonResult(string sCarrier)
        {
            try
            {
            string url = ConfigurationManager. AppSettings["ImtCallHomeURIString"].ToString(); 
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UseDefaultCredentials = true;
            request.PreAuthenticate = true;
            request.Credentials = CredentialCache.DefaultCredentials;

            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";

            var result = GetImtJsonResult(sCarrier);

            byte[] byteArray = Encoding.UTF8.GetBytes(result);
            request.ContentLength = byteArray.Length;

            Stream dataStream = request.GetRequestStream();
            dataStream.Write(byteArray, 0, byteArray.Length);
            dataStream.Close();

            WebResponse response = (HttpWebResponse)request.GetResponse();
            dataStream = response.GetResponseStream();

            StreamReader reader = new StreamReader(dataStream);
            var data = reader.ReadToEnd();

            reader.Close();
            dataStream.Close();
            response.Close();

            }
            catch (System.Exception ex)
            {
                err.WriteSystemEntry(true, ex, 
                    "ImtCallHome.PostJsonResult:  Carrier = " + sCarrier);
            }
        }

        public string GetImtJsonResult(string sCarrier)
        {
            var jsonResult = new StringBuilder();
            bool anExists = false;
            SqlDataReader oSqlDR;

            SqlCommand oSqlCmd = new SqlCommand();
            SqlConnection oSqlConn = new SqlConnection();
	    //increase default timeout of 30secs to 3 minutes
            oSqlCmd.CommandTimeout = 180;   
            var result = "";
            try
            {
                oSqlConn.ConnectionString = db.GetConnection(sCarrier);
                oSqlConn.Open();
                oSqlCmd.Connection = oSqlConn;
                oSqlCmd.CommandType = CommandType.StoredProcedure;
                oSqlCmd.CommandText = "dbo.uspUIImtCallHomeAsJSON";
                oSqlCmd.Parameters.Clear();
                oSqlDR = oSqlCmd.ExecuteReader();

                var options = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };

                if (!oSqlDR.HasRows)
                {
                    result.Insert(0, "{}");
                }
                else
                {
                    while (oSqlDR.Read())
                    {
                        jsonResult.Append(oSqlDR.GetValue(0).ToString());
                        anExists = AgencyNumberExists(sCarrier);

                        JArray arr = JArray.Parse("[" + jsonResult + "]");
                        foreach (JObject obj in arr.Children<JObject>())
                        {
                            foreach (JProperty  singleProp in obj.Properties())
                            {
                                string name = singleProp.Name;
                                string originalValue = singleProp.
                                                    Value.ToString();

                                if (name.ToString().
                                    Equals("agent_rater") && anExists)
                                {
                                    jsonResult.Replace("\"agent_rater\":\"0\"", "\"agent_rater\":\"1\"");
                                }
                            }
                        }

                        var jsonElement = JsonSerializer.Deserialize<JsonElement> (jsonResult.ToString());
                        result = JsonSerializer.Serialize(jsonElement, options);
                    }
                }
                oSqlCmd.Dispose();
            }
            catch (System.Exception ex)
            {
                err.WriteSystemEntry(true, ex, "ImtCallHome.GetImtJsonResult:  Carrier = " + sCarrier);
            }
            finally
            {
                if (oSqlConn.State != ConnectionState.Closed)
                    oSqlConn.Close();
                oSqlConn.Dispose();
            }
            return result;
        }

        bool AgencyNumberExists(string sCarrier)
        {
            bool isExist = false;
            string sSql = "";
            SqlDataReader oSqlDR;
            SqlCommand oSqlCmd = new SqlCommand();
            SqlConnection oSqlConn = new SqlConnection();
            try
            {

                oSqlConn.ConnectionString = db.GetConnection("Rating");
                oSqlConn.Open();
                oSqlCmd.Connection = oSqlConn;
                oSqlCmd.Parameters.Clear();
                oSqlCmd.CommandType = CommandType.Text;
		 //increase default timeout of 30secs to 3 minutess
                oSqlCmd.CommandTimeout = 180;   

                sSql = "SELECT COUNT(DISTINCT A.[AGENCY NUMBER])"+ "AS NumRatingAccounts" + vbCRLF;
                sSql = sSql + "FROM CUSTOMER.DBO.SPECTRUMCARRIERINSTANCE" +  " AS SC 			      		WITH(NOLOCK)" + vbCRLF;
                sSql = sSql + "INNER JOIN [Customer Database].DBO.AGENCIES1" + 
                                " AS A WITH(NOLOCK)" + vbCRLF;
                sSql = sSql + " ON SC.PriorityrateView = A.PriorityRateView" + vbCRLF;
                sSql = sSql + " WHERE left([Agency Number], 1) NOT IN('0', '1'," + "'2', '6', '9')" + vbCRLF;
                sSql = sSql + "and sc.SpectrumCarrierInstance = '" + sCarrier + "'" + vbCRLF;
                sSql = sSql + "group by sc.SpectrumCarrierInstance";
                oSqlCmd.CommandText = sSql;

                oSqlDR = oSqlCmd.ExecuteReader();

                if (oSqlDR.HasRows)
                {
                    isExist = true;
                }
                oSqlCmd.Dispose();

            }
            catch (System.Exception ex)
            {
                err.WriteSystemEntry(true, ex, 
                    "ImtCallHome.AgencyNumberExists:  Carrier = " + 	sCarrier);
            }
            finally
            {
                if (oSqlConn.State != ConnectionState.Closed)
                    oSqlConn.Close();
                oSqlConn.Dispose();

            }
            return isExist;
        }

    }
}

