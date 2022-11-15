using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace SpectrumServices
{
    public class DB
    {

        ErrorLog err = new ErrorLog();



        public string GetConnection(string sInstance)
        {
            string sConnectionString = "";
            ConnectionStringSettings mySetting = new ConnectionStringSettings();
            mySetting = null;

            try 
            {

                mySetting = ConfigurationManager. ConnectionStrings[sInstance];


                if ((mySetting == null) || (string.IsNullOrEmpty(mySetting.ConnectionString)))
                {
                    err.WriteStringEntry(true, "Spectrum Services is Missing a Connection String for " + sInstance);
                }
                else
                {
                    sConnectionString = mySetting.ConnectionString;
                }
            }
            catch (Exception ex)
            {
                err.WriteSystemEntry(true, ex, "DB.GetConnection for :: " + sInstance);
                sConnectionString = "";
            }
            return sConnectionString;
        }

        public List<String> ReportAllCarriers()
        {
            List<String> carrierList = new List<String>();
            SqlDataReader oSqlDR;
            SqlCommand oSqlCmd = new SqlCommand();
            SqlConnection oSqlConn = new SqlConnection();
            var sSql = "";
            try
            {
                oSqlConn.ConnectionString  =   GetConnection("Rating");
                oSqlConn.Open();
                oSqlCmd.Connection = oSqlConn;
                oSqlCmd.Parameters.Clear();
                oSqlCmd.CommandType = CommandType.Text;

                sSql = "select distinct SpectrumCarrierInstance from  [Customer].[dbo].[SpectrumCarrierInstance] with 				(nolock)";
                oSqlCmd.CommandText = sSql;
                oSqlDR = oSqlCmd.ExecuteReader();

                while (oSqlDR.Read())
                {
                 carrierList.Add(oSqlDR.GetValue(0).ToString());
                }

                oSqlCmd.Dispose();
            }
            catch(Exception ex)
            {
                err.WriteSystemEntry(true, ex);
            }
            finally
            {
                if (oSqlConn.State != ConnectionState.Closed)
                    oSqlConn.Close();
                oSqlConn.Dispose();
            }
            return carrierList;
        }

        public string CheckMasterConfig(string sCarrier, string 		settingName)
        {
            string settingValue = "";
            SqlDataReader oSqlDR;
            SqlCommand oSqlCmd = new SqlCommand();
            SqlConnection oSqlConn = new SqlConnection();
            var sSql = "";
            try
            {
                oSqlConn.ConnectionString = 	   		  	    			GetConnection(sCarrier);
                oSqlConn.Open();
                oSqlCmd.Connection = oSqlConn;
                oSqlCmd.Parameters.Clear();
                oSqlCmd.CommandType = CommandType.Text;

                sSql = "select MC.SettingValue FROM [dbo].[MasterConfig] AS  MC with (nolock) where MC.SettingName 			='" + settingName 	+ "'";
                oSqlCmd.CommandText = sSql;
                oSqlDR = oSqlCmd.ExecuteReader();

                while (oSqlDR.Read())
                {
                    settingValue =oSqlDR.GetValue(0).ToString();
                }
                oSqlCmd.Dispose();
            }
            catch (Exception ex)
            {
                err.WriteSystemEntry(true, ex);
            }
            finally
            {
                if (oSqlConn.State != ConnectionState.Closed)
                    oSqlConn.Close();
                oSqlConn.Dispose();
            }

            return settingValue;
        }


    }  //  end class

}    //  end namespace

