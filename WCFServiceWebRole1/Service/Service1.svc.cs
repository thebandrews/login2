using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Configuration;

//using Microsoft.WindowsAzure;
//using Microsoft.WindowsAzure.StorageClient;
//using Microsoft.WindowsAzure;

using AzureStorage = Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;


//using Microsoft.WindowsAzure.Storage.CloudStorageAccount

//using WCFServiceWebRole1.Database;
//using WCFServiceWebRole1.Helpers;

using System.Net;
using System.IO;
//using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure;
using WCFServiceWebRole1.Database;
using HtmlAgilityPack;
using System.Diagnostics;

namespace WCFServiceWebRole1
{
    public class Service1 : IService1
    {
        public string GetHello()
        {

            // Retrieve storage account from connection string            
            AzureStorage.CloudStorageAccount storageAccount = AzureStorage.CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("FooBarConnectionString"));

            // Create the table client
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();



            // Create the table if it doesn't exist
            CloudTable table = tableClient.GetTableReference("users9");
            table.CreateIfNotExists();


            return "Hello from my WCF service in Windows Azure!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loginUrl">Login url</param>
        /// <param name="userName">User's login name</param>
        /// <param name="password">User's password</param>
        /// <param name="formParams">Optional string which contains any additional form params</param>
        /// <returns></returns>
        public bool InitUser(String userName, String password)
        {
            Trace.WriteLine("InitUser called", "Information");

            //////////////////////////////////////////////////////////////////////////////
            //
            // local variables
            // TODO: Review if there is a better solution to hard-coding these
            //             
            string formParams = string.Format("language=en&affiliateName=espn&parentLocation=&registrationFormId=espn");
            string loginUrl = "https://r.espn.go.com/members/util/loginUser";
            CookieCollection cookies;
            string leagueId = "";
            string teamId = "";
            string season = "";
            string userTable = "users";
            string teamTable = "teams";
            string cookieTable = "cookies";
            bool newUser = true;


            /////////////////////////////////////////////////////////////////////////////
            //
            // Lookup / Store user in storage.
            //
            // Encrypt the user password for safe persistence
            // TODO: Store the key somewhere safe. Also, it may be better to store password on the device?
            //
            string passwordHash = Crypto.SHA512Hash.ComputeHash(password, null);

            // Retrieve storage account from connection string
            AzureStorage.CloudStorageAccount storageAccount = AzureStorage.CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("FooBarConnectionString"));

            // Create the table client
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            //
            // Create the user/team/cookie tables if they don't exist
            //
            // Create the table if it doesn't exist
            tableClient.GetTableReference(userTable).CreateIfNotExists();
            tableClient.GetTableReference(teamTable).CreateIfNotExists();
            tableClient.GetTableReference(cookieTable).CreateIfNotExists();

            //
            // Create the CloudTable ojbect that represents the "users" table.
            //
            CloudTable user_table = tableClient.GetTableReference(userTable);

            //
            // Create a retrieve operation that takes a customer entity.
            //
            TableOperation retrieveOperation = TableOperation.Retrieve<UserEntity>("key", userName);

            //
            // Execute the retrieve operation.
            //
            TableResult retrievedResult = user_table.Execute(retrieveOperation);


            //
            // Check to see if password is correct
            //
            if (retrievedResult.Result != null)
            {
                UserEntity user = (UserEntity)retrievedResult.Result;

                if (Crypto.SHA512Hash.VerifyHash(password, user.PasswordHash))
                {
                    newUser = false;
                }

            }

            //
            // If this is a new user add them to the users databse
            //           
            if (newUser)
            {
                //
                // Setup formUrl string
                //
                string formUrl = "";

                if (!String.IsNullOrEmpty(formParams))
                {
                    formUrl = formParams + "&";
                }
                formUrl += string.Format("username={0}&password={1}", userName, password);


                //
                // Now setup the POST request
                //
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(loginUrl);
                req.CookieContainer = new CookieContainer();
                req.ContentType = "application/x-www-form-urlencoded";
                req.Method = "POST";

                byte[] bytes = Encoding.ASCII.GetBytes(formUrl);
                req.ContentLength = bytes.Length;

                using (Stream os = req.GetRequestStream())
                {
                    os.Write(bytes, 0, bytes.Length);
                }


                //
                // Read the response
                //
                string respString = "";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    //
                    // Gather cookie info
                    //
                    cookies = resp.Cookies;


                    //
                    // Read the response stream to determine if login was successful.
                    //
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        Char[] buffer = new Char[256];
                        int count = reader.Read(buffer, 0, 256);
                        while (count > 0)
                        {
                            respString += new String(buffer, 0, count);
                            count = reader.Read(buffer, 0, 256);
                        }

                    } // reader is disposed here

                }

                //writeLog(respString);

                //
                // If response string contains "login":"true" this indicates success
                //
                if (!respString.Contains("{\"login\":\"true\"}"))
                {
                    throw new InvalidOperationException("Failed to login with given credentials");
                }


                //
                // Login success - Now save user to databse
                // TODO: Figure out a better partition key other than "key"
                //

                UserEntity user = new UserEntity("key", userName);
                user.PasswordHash = passwordHash;

                //
                // Add the new team to the teams table
                // Create the TableOperation that inserts the customer entity.
                //
                TableOperation insertOperation = TableOperation.Insert(user);


                //
                // Execute the insert operation.
                //
                user_table.Execute(insertOperation);

                //////////////////////////////////////////////////////////////////////////////////
                //
                // Now that we have logged in, extract user info from the page
                //
                string url2 = "http://games.espn.go.com/frontpage/football";

                HttpWebRequest req2 = (HttpWebRequest)WebRequest.Create(url2);
                req2.CookieContainer = new CookieContainer();
                req2.ContentType = "application/x-www-form-urlencoded";
                req2.Method = "POST";


                //
                // Create the CloudTable ojbect that represents the "users" table.
                //
                CloudTable cookie_table = tableClient.GetTableReference(cookieTable);

                //
                // Add the login cookies
                //
                foreach (Cookie cook in cookies)
                {
                    //writeLog("**name = " + cook.Name + " value = " + cook.Value);
                    CookieEntity cookie_entity1 = new CookieEntity(userName, cook.Name);
                    cookie_entity1.Value = cook.Value;
                    cookie_entity1.Path = cook.Path;
                    cookie_entity1.Domain = cook.Domain;

                    //
                    // Add the new team to the teams table
                    // Create the TableOperation that inserts the customer entity.
                    //
                    insertOperation = TableOperation.Insert(cookie_entity1);


                    //
                    // Execute the insert operation.
                    //
                    cookie_table.Execute(insertOperation);

                    //
                    // Add the cookie to the cookie container
                    //
                    req2.CookieContainer.Add(cook);
                }


                //
                // Read the response
                //            
                using (HttpWebResponse resp = (HttpWebResponse)req2.GetResponse())
                {

                    //
                    // Do something with the response stream. As an example, we'll 
                    // stream the response to the console via a 256 character buffer 
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                    {
                        HtmlDocument doc = new HtmlDocument();
                        doc.Load(reader);

                        //
                        // Build regEx for extracting legue info from link address
                        //
                        string pattern = @"(clubhouse\?leagueId=)(\d+)(&teamId=)(\d+)(&seasonId=)(\d+)";
                        Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);


                        //
                        // Create the CloudTable ojbect that represents the "users" table.
                        //
                        CloudTable team_table = tableClient.GetTableReference(teamTable);

                        foreach (HtmlNode hNode in doc.DocumentNode.SelectNodes("//a[@href]"))
                        {

                            HtmlAttribute att = hNode.Attributes["href"];
                            Match match = rgx.Match(att.Value);

                            //
                            // If our regEx finds a match then we have leagueId, teamId, and seasonId
                            //
                            if (match.Success)
                            {
                                //writeLog("NODE> Name: " + hNode.Name + ", ID: " + hNode.Id + "attr: " + att.Value);


                                GroupCollection groups = match.Groups;

                                leagueId = groups[2].Value;
                                teamId = groups[4].Value;
                                season = groups[6].Value;

                                //
                                // Create a new team entity
                                //
                                TeamEntity team_entity1 = new TeamEntity(leagueId, teamId);

                                team_entity1.Season = season;
                                team_entity1.UserId = userName;

                                // Add the new team to the teams table
                                insertOperation = TableOperation.Insert(team_entity1);

                                //
                                // Execute the insert operation.
                                //
                                team_table.Execute(insertOperation);
                            }

                        }

                    } // end using streamreader
                } // end using httpwebresponse
            }// end if(newuser)

            return true;
        }


        /// <summary>
        /// 
        /// </summary>        
        /// <returns></returns>
        public bool SyncData(String userName)
        {

            string userTable = "users";
            string teamTable = "teams";
            string cookieTable = "cookies";

            CookieContainer cookies = new CookieContainer();


            //
            // Retrieve storage account from connection string
            //
            AzureStorage.CloudStorageAccount storageAccount = AzureStorage.CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("FooBarConnectionString"));

            //
            // Create the table client
            //
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();


            //
            // Create the user/team/cookie tables if they don't exist
            //            
            tableClient.GetTableReference(userTable).CreateIfNotExists();
            tableClient.GetTableReference(teamTable).CreateIfNotExists();
            tableClient.GetTableReference(cookieTable).CreateIfNotExists();

            //
            // Create the CloudTable ojbect that represents the "cookies" table.
            //
            CloudTable cookie_table = tableClient.GetTableReference(cookieTable);


            //
            // Create the table query.
            //
            TableQuery<CookieEntity> rangeQuery = new TableQuery<CookieEntity>().Where(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, userName));


            //
            // Loop through the results, building up cookie collection
            //
            foreach (CookieEntity entity in cookie_table.ExecuteQuery(rangeQuery))
            {
                Cookie cook = new Cookie();

                cook.Value = entity.Value;
                cook.Path = entity.Path;
                cook.Domain = entity.Domain;

                cookies.Add(cook);

            }


            //
            // Open Scoreboard page
            //
            string url2 = "http://games.espn.go.com/ffl/scoreboard?leagueId=549477&seasonId=2012";

            HttpWebRequest req2 = (HttpWebRequest)WebRequest.Create(url2);
            req2.CookieContainer = cookies;
            req2.ContentType = "application/x-www-form-urlencoded";
            req2.Method = "POST";

            return true;
        }
    }
}
