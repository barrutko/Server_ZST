using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System.Security.Permissions;

namespace ZST_Server
{
    class Program
    {
        private static Mutex mutex = new Mutex();
        private String prefix;
        private string databaseConnectionString;
        private SqlConnection databaseConnection;


        static void Main(string[] args)
        {
            Program server = new Program();

            server.setPrefix(args[0]);
            //server.setPrefix("localhost");

            //Setting database connection
            server.databaseConnectionString = ConfigurationManager.ConnectionStrings["ZST_Server.Properties.Settings.DatabaseConnectionString"].ConnectionString;
            server.databaseConnection = new SqlConnection(server.databaseConnectionString);

            //Starting httpListener
            Thread httpListenerThread = new Thread(() => server.HttpListener(server.getPrefix()));
            httpListenerThread.Start();
            
            SqlCommand mainSqlCommand;
            string queryString;
            string userDecision;
            Console.WriteLine("ZST_Server started!");

            while (true)
            {
                Console.WriteLine("1. Show declared event definitions\n2. Browse event logs\n3. Show Client-Server activity journal\n4. Delete logs or definitions");

                userDecision = "0";
                while (!userDecision.Equals("1") && !userDecision.Equals("2") && !userDecision.Equals("3") && !userDecision.Equals("4"))
                {
                    Console.WriteLine("Enter 1, 2, 3 or 4.");                    
                    userDecision = Console.ReadLine();                    
                }

                switch (userDecision)
                {
                    
                    case "1": //Show declared definitions 
                        queryString = "SELECT COUNT(*) FROM Definitions";
                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                        mutex.WaitOne();
                        server.databaseConnection.Open();

                        if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                        {
                            queryString = "SELECT Type, Definition FROM Definitions;";
                            mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                            SqlDataReader dataReader = mainSqlCommand.ExecuteReader();

                            Console.WriteLine("\nDefinition list:");

                            while (dataReader.Read())
                            {
                                IDataRecord record = (IDataRecord)dataReader;
                                Console.WriteLine(String.Format("{0}: {1}", record[0], record[1]));
                            }
                        }
                        else
                        {
                            Console.WriteLine("\nDefinition list is empty.\n");
                        }
                        Console.WriteLine("");
                        server.databaseConnection.Close();
                        mutex.ReleaseMutex();
                        break;

                    case "2": //Browse event logs
                        queryString = "SELECT COUNT(*) FROM Definitions";
                        mutex.WaitOne();
                        server.databaseConnection.Open();
                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                        if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                        {
                            queryString = "SELECT Type FROM Definitions;";
                            mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                            SqlDataReader dataReader = mainSqlCommand.ExecuteReader();
                            string typeSequence = null;
                            while (dataReader.Read())
                            {
                                IDataRecord record = (IDataRecord)dataReader;
                                typeSequence += String.Format("{0} ", record[0]);
                            }
                            string[] typeList = typeSequence.Split();
                            server.databaseConnection.Close();
                            server.databaseConnection.Open();
                            Console.WriteLine("\nEvent log of which type should be showed? (List below)");

                            bool noRecords = true;

                            for (int i = 0; i < typeList.Length - 1; i++)
                            {

                                queryString = "SELECT COUNT(*) FROM " + typeList.GetValue(i);
                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                {

                                    Console.WriteLine(typeList.GetValue(i));
                                    noRecords = false;
                                }

                            }

                            if (noRecords)
                            {
                                Console.WriteLine("Error. No logs in database.");
                            }
                            else
                            {
                                Console.WriteLine("");
                                userDecision = "";
                                while (true)
                                {
                                    Console.WriteLine("Enter valid type name:");
                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    userDecision = Console.ReadLine();
                                    mutex.WaitOne();
                                    server.databaseConnection.Open();
                                    queryString = "SELECT COUNT(*) FROM " + userDecision;
                                    mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                    if (typeList.Contains(userDecision))
                                        if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                            break;
                                }
                                string chosenEvent = userDecision;
                                queryString = "SELECT Definition FROM Definitions WHERE Type='" + userDecision + "';";
                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                dataReader = mainSqlCommand.ExecuteReader();

                                string definitionString = "";

                                dataReader.Read();
                                IDataRecord record = (IDataRecord)dataReader;
                                definitionString += String.Format("{0}", record[0]);
                                int numberOfLabels = definitionString.Split().Length;
                                string[] labels = definitionString.Split();
                                server.databaseConnection.Close();
                                mutex.ReleaseMutex();
                                Console.WriteLine("\nChoose filter from below:");
                                userDecision = "";
                                Console.WriteLine("NONE\nEventID\nClientID\nDate");
                                foreach (string label in labels)
                                {
                                    Console.WriteLine(label);
                                }
                                while (!labels.Contains(userDecision) && !userDecision.Equals("NONE") && !userDecision.Equals("EventID") && !userDecision.Equals("ClientID") && !userDecision.Equals("Date"))
                                {
                                    Console.WriteLine("Enter valid label name");
                                    userDecision = Console.ReadLine();
                                }
                                string chosenFilter = userDecision;
                                string chosenFilterValue = "";
                                if (chosenFilter != "NONE")
                                {
                                    Console.WriteLine("Enter filter label value");
                                    userDecision = Console.ReadLine();
                                    chosenFilterValue = userDecision;
                                }

                                queryString = "SELECT * FROM " + chosenEvent;
                                if (chosenFilter != "NONE")
                                {
                                    if (chosenFilter == "EventID" || chosenFilter == "ClientID")
                                    {
                                        queryString += " WHERE " + chosenFilter + "=" + chosenFilterValue;
                                    }
                                    else if (chosenFilter == "Date")
                                    {
                                        queryString += " WHERE " + chosenFilter + " LIKE '%" + chosenFilterValue + "%'";
                                    }
                                    else
                                    {
                                        queryString += " WHERE " + chosenFilter + "='" + chosenFilterValue + "'";
                                    }
                                }

                                Console.WriteLine("\nChoose sort order from below:");
                                Console.WriteLine("EventID (works identical as date)\nClientID");
                                foreach (string label in labels)
                                {
                                    Console.WriteLine(label);
                                }
                                while (!labels.Contains(userDecision) && !userDecision.Equals("EventID") && !userDecision.Equals("ClientID"))
                                {
                                    Console.WriteLine("Enter valid label name");
                                    userDecision = Console.ReadLine();
                                }
                                string chosenSortLabel = userDecision;

                                queryString += " ORDER BY " + chosenSortLabel;

                                while (!userDecision.Equals("A") && !userDecision.Equals("D"))
                                {
                                    Console.WriteLine("Ascending or descending? (A/D)");
                                    userDecision = Console.ReadLine();
                                }
                                string chosenOrder = userDecision;

                                if (chosenOrder.Equals("A"))
                                    queryString += " ASC;";
                                else
                                    queryString += " DESC;";
                                
                                

                                mutex.WaitOne();
                                server.databaseConnection.Open();


                                
                                    

                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                dataReader = mainSqlCommand.ExecuteReader();
                                Console.Write("\n");

                                string typeOfEventToDelete = userDecision;

                                while (dataReader.Read())
                                {
                                    record = (IDataRecord)dataReader;
                                    Console.Write(String.Format("{0}:{1}; {2};", record[0], record[1], record[2]));

                                    for (int i = 0; i < numberOfLabels; i++)
                                        Console.Write(String.Format(" {0};", record[i + 3]));
                                    Console.WriteLine("");
                                }


                            }

                            server.databaseConnection.Close();
                            mutex.ReleaseMutex();


                        }
                        else
                        {
                            server.databaseConnection.Close();
                            mutex.ReleaseMutex();
                            Console.WriteLine("\nDefinition list is empty.\n");
                        }
                        Console.WriteLine("\n");
                        break;

                       
                    case "3": //Show Client-Server activity
                       
                        queryString = "SELECT COUNT(*) FROM ClientServerActivity";
                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                        mutex.WaitOne();
                        server.databaseConnection.Open();
                        if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                        {

                            server.databaseConnection.Close();
                            mutex.ReleaseMutex();
                            userDecision = "0";
                            Console.WriteLine("\nHow many activity logs should be shown?\n1. All\n2. Up to last 5");
                            while (!userDecision.Equals("1") && !userDecision.Equals("2"))
                            {
                                Console.WriteLine("Enter 1 or 2.");
                                userDecision = Console.ReadLine();
                            }
                            if(userDecision == "1")
                                queryString = "SELECT Activity FROM ClientServerActivity ORDER BY Id DESC;";
                            else
                                queryString = "SELECT TOP 5 Activity FROM ClientServerActivity ORDER BY Id DESC;";

                            mutex.WaitOne();
                            server.databaseConnection.Open();

                            mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                            SqlDataReader dataReader = mainSqlCommand.ExecuteReader();

                            Console.WriteLine("\nActivity list:");

                            while (dataReader.Read())
                            {
                                IDataRecord record = (IDataRecord)dataReader;
                                Console.WriteLine(String.Format("{0}", record[0]));
                            }
                        }
                        else
                        {
                            Console.WriteLine("\nActivity list is empty.\n");
                        }
                        Console.WriteLine("");
                        server.databaseConnection.Close();
                        mutex.ReleaseMutex();
                        break;

                    case "4": //Delete things
                        userDecision = "0";
                        Console.WriteLine("\nWhat to delete?\n1. Event definition\n2. Event log\n3. Clear Client-Server activity journal");
                        while (!userDecision.Equals("1") && !userDecision.Equals("2") && !userDecision.Equals("3"))
                        {
                            Console.WriteLine("Enter 1, 2 or 3.");
                            userDecision = Console.ReadLine();
                        }
                        
                        switch(userDecision)
                        {
                            case "1": //Delete whole definition
                                queryString = "SELECT COUNT(*) FROM Definitions";
                                mutex.WaitOne();
                                server.databaseConnection.Open();
                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                {
                                    Console.WriteLine("\nDefinition list:");
                                    queryString = "SELECT Type FROM Definitions ORDER BY Type DESC;";
                                    mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                    SqlDataReader dataReader = mainSqlCommand.ExecuteReader();
                                    string typeSequence = null;
                                    while (dataReader.Read())
                                    {
                                        IDataRecord record = (IDataRecord)dataReader;
                                        Console.WriteLine(String.Format("{0}", record[0]));
                                        typeSequence += String.Format("{0} ", record[0]);                                        
                                    }
                                    string[] typeList = typeSequence.Split();
                                    bool validTypeEntered = false;
                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    while(!validTypeEntered)
                                    {
                                        Console.WriteLine("\nEnter valid type name from the list above");                                        
                                        userDecision = Console.ReadLine();
                                        foreach (string type in typeList)
                                        {
                                            if (type.Equals(userDecision))
                                                validTypeEntered = true;
                                        }
                                    }
                                    mutex.WaitOne();
                                    server.databaseConnection.Open();
                                    queryString = "DELETE FROM Definitions WHERE Type='" + userDecision + "';";
                                    mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                    mainSqlCommand.ExecuteScalar();
                                    queryString = "DROP TABLE " + userDecision + ";";
                                    mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                    mainSqlCommand.ExecuteScalar();
                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    Console.WriteLine("\nDefinition of " + userDecision +" is now erased\n");


                                }
                                else
                                {
                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    Console.WriteLine("\nDefinition list is already empty.\n");
                                }
                                
                                break;

                            case "2": //Delete log
                                queryString = "SELECT COUNT(*) FROM Definitions";
                                mutex.WaitOne();
                                server.databaseConnection.Open();
                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                {
                                    queryString = "SELECT Type FROM Definitions;";
                                    mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                    SqlDataReader dataReader = mainSqlCommand.ExecuteReader();
                                    string typeSequence = null;
                                    while (dataReader.Read())
                                    {
                                        IDataRecord record = (IDataRecord)dataReader;
                                        typeSequence += String.Format("{0} ", record[0]);
                                    }
                                    string[] typeList = typeSequence.Split();
                                    server.databaseConnection.Close();
                                    server.databaseConnection.Open();
                                    Console.WriteLine("Event log of which type should be deleted? (List below)");

                                    bool noRecords = true;

                                    for (int i = 0; i<typeList.Length-1; i++)
                                    {

                                        queryString = "SELECT COUNT(*) FROM " + typeList.GetValue(i);
                                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                        if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                        {

                                            Console.WriteLine(typeList.GetValue(i));
                                            noRecords = false;
                                        }

                                    }

                                    if (noRecords)
                                    {
                                        Console.WriteLine("Error. No logs in database.");
                                    }
                                    else
                                    {

                                        userDecision = "";
                                        while (true)
                                        {
                                            Console.WriteLine("Enter valid type name:");
                                            server.databaseConnection.Close();
                                            mutex.ReleaseMutex();
                                            userDecision = Console.ReadLine();
                                            mutex.WaitOne();
                                            server.databaseConnection.Open();
                                            queryString = "SELECT COUNT(*) FROM " + userDecision;
                                            mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);

                                            if (typeList.Contains(userDecision))
                                                if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                                    break;
                                        }

                                        queryString = "SELECT Definition FROM Definitions WHERE Type='" + userDecision + "';";
                                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                        dataReader = mainSqlCommand.ExecuteReader();

                                        string definitionString = "";

                                        dataReader.Read();
                                        IDataRecord record = (IDataRecord)dataReader;
                                        definitionString += String.Format("{0}", record[0]);
                                        int numberOfLabels = definitionString.Split().Length;
                                        server.databaseConnection.Close();
                                        server.databaseConnection.Open();

                                        queryString = "SELECT * FROM " + userDecision + ";";
                                        mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                        dataReader = mainSqlCommand.ExecuteReader();
                                        Console.Write(numberOfLabels+"\n");

                                        string typeOfEventToDelete = userDecision;

                                        while (dataReader.Read())
                                        {
                                            record = (IDataRecord)dataReader;
                                            Console.Write(String.Format("{0}:{1}; {2};", record[0], record[1], record[2]));

                                            for (int i = 0; i<numberOfLabels; i++)
                                                Console.Write(String.Format(" {0};", record[i+3]));
                                            Console.WriteLine("");
                                        }

                                        while (true)
                                        {
                                            server.databaseConnection.Close();
                                            mutex.ReleaseMutex();
                                            Console.WriteLine("\nEnter valid ID of event that should be deleted");
                                            userDecision = Console.ReadLine();
                                            mutex.WaitOne();
                                            server.databaseConnection.Open();
                                            int eventID;
                                            bool parseSucces = Int32.TryParse(userDecision,out eventID);

                                            if (!parseSucces)
                                                continue;

                                            queryString = "SELECT COUNT(*) FROM " + typeOfEventToDelete + " WHERE EventID=" + eventID + ";";
                                            
                                            mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                            if (server.recordExists((int)mainSqlCommand.ExecuteScalar()))
                                            {
                                                server.databaseConnection.Close();
                                                server.databaseConnection.Open();
                                                queryString = "DELETE FROM " + typeOfEventToDelete + " WHERE EventID=" + eventID + ";";
                                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                                mainSqlCommand.ExecuteNonQuery();
                                                break;
                                            }
                                        }
                                        Console.WriteLine("Event log removed successfully.\n");
                                    }

                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    

                                }
                                else
                                {
                                    server.databaseConnection.Close();
                                    mutex.ReleaseMutex();
                                    Console.WriteLine("\nDefinition list is empty.\n");
                                }
                                break;


                            case "3": //Delete Client-Server activity
                                mutex.WaitOne();
                                server.databaseConnection.Open();
                                queryString = "DELETE FROM ClientServerActivity;";
                                mainSqlCommand = new SqlCommand(queryString, server.databaseConnection);
                                mainSqlCommand.ExecuteScalar();
                                server.databaseConnection.Close();
                                mutex.ReleaseMutex();
                                Console.WriteLine("\nAll Client-Server activity history is now erased.\n");
                                break;

                            default:
                                break;

                        }

                        break;
                       
                    default:
                        break;

                        
                }
                
                
                
            }
        }

        private void setPrefix(string v)
        {
            prefix = "http://" + v + ":14880/";
        }

        public void HttpListener(string prefix)
        {
            SqlCommand listenerSqlCommand;
            String queryString;
            while (true)
            {
                if (prefix == null)
                    throw new ArgumentException("Prefix needed");

                HttpListener listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();

                HttpListenerContext context = listener.GetContext();

                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                Stream output = response.OutputStream;
                String messageType = request.Headers.Get("X-Message-Type");
                Stream input = request.InputStream;
                StreamReader reader = new StreamReader(input);
                string requestString = reader.ReadToEnd();

                switch (messageType)
                {
                    case "EventLog":
                        queryString = "SELECT COUNT(*) FROM Definitions WHERE Type = '" + request.Headers.Get("X-Event-Type") + "'";
                        listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                        mutex.WaitOne();
                        databaseConnection.Open();
                        if (recordExists((int)listenerSqlCommand.ExecuteScalar()))
                        {
                            queryString = "SELECT Definition FROM Definitions WHERE Type = '" + request.Headers.Get("X-Event-Type") + "'";
                            listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                            SqlDataReader dataReader = listenerSqlCommand.ExecuteReader();
                            string definitionString = null;
                            while (dataReader.Read())
                            {
                                IDataRecord record = (IDataRecord)dataReader;
                                definitionString += String.Format("{0}", record[0]);
                            }
                            string[] requestSequence = requestString.Split(' ');
                            string[] definitionSequence = definitionString.Split(' ');

                            if (requestSequence.Length == definitionSequence.Length)
                            {
                                queryString = "INSERT INTO " + request.Headers.Get("X-Event-Type") + " VALUES ('" + request.Headers.Get("X-Client-ID") + "', '" + request.Headers.Get("Date");
                               
                                foreach(string parameter in requestSequence)
                                {
                                    queryString += "', '";
                                    queryString += parameter;
                                }
                                queryString += "');";

                                dataReader.Close();

                                listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                                listenerSqlCommand.ExecuteScalar();

                                String clientServerActivity = "Received new event from " + request.Headers.Get("X-Client-ID") + ". (" + request.Headers.Get("Date") + ")";
                                queryString = "INSERT INTO ClientServerActivity VALUES ('" + clientServerActivity + "');";
                                listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                                listenerSqlCommand.ExecuteScalar();


                            }
                            else
                            {
                                response.StatusCode = 482;
                            }
                        }
                        else
                        {
                            response.StatusCode = 481;
                        }
                        databaseConnection.Close();
                        
                        mutex.ReleaseMutex();
                        break;

                    case "NewDefinition":
                       
                        queryString = "SELECT COUNT(*) FROM Definitions WHERE Type = '" + request.Headers.Get("X-Event-Type") + "'";
                        listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                        mutex.WaitOne();
                        databaseConnection.Open();
                        if (recordExists((int)listenerSqlCommand.ExecuteScalar()))
                        {
                            response.StatusCode = 400;
                        }
                        else
                        {
                            queryString = "INSERT INTO Definitions VALUES ('" + request.Headers.Get("X-Event-Type") + "', '" + requestString + "');";
                            listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                            listenerSqlCommand.ExecuteScalar();

                            String clientServerActivity = "Received new definition from " + request.Headers.Get("X-Client-ID") + ". (" + request.Headers.Get("Date") + ")";
                            queryString = "INSERT INTO ClientServerActivity VALUES ('" + clientServerActivity + "');";
                            listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                            listenerSqlCommand.ExecuteScalar();

                            string[] definitionSequence = requestString.Split();

                            queryString = "CREATE TABLE " + request.Headers.Get("X-Event-Type") + " (EventID INT NOT NULL PRIMARY KEY IDENTITY, ClientID INT NOT NULL, Date NVARCHAR(50) NOT NULL";
                            foreach (string label in definitionSequence)
                            {
                                queryString += ", ";
                                queryString += label;
                                queryString += " NVARCHAR(50) NOT NULL";
                            }
                            queryString += ");";
                            listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                            listenerSqlCommand.ExecuteNonQuery();
                        }
                        databaseConnection.Close();
                        mutex.ReleaseMutex();
                        break;

                    case "DefinitionInfo":
                        queryString = "SELECT COUNT(*) FROM Definitions";
                        listenerSqlCommand = new SqlCommand(queryString, this.databaseConnection);
                        mutex.WaitOne();
                        databaseConnection.Open();
                        if (recordExists((int)listenerSqlCommand.ExecuteScalar()))
                        {

                            queryString = "SELECT Type, Definition FROM Definitions;";
                            listenerSqlCommand = new SqlCommand(queryString, databaseConnection);

                            SqlDataReader dataReader = listenerSqlCommand.ExecuteReader();
                            String responseString = null;
                            while (dataReader.Read())
                            {
                                IDataRecord record = (IDataRecord)dataReader;
                                responseString += String.Format("{0}: {1}\n", record[0], record[1]);
                            }
                            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                            response.ContentLength64 = buffer.Length;
                            output = response.OutputStream;
                            output.Write(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            response.StatusCode = 400;
                        }
                        databaseConnection.Close();
                        mutex.ReleaseMutex();
                        break;

                    default:
                        response.StatusCode = 483;
                        break;
                        
                }
                output.Close();
                listener.Stop();
            }
        }

        public String getPrefix()
        {
                return prefix;
        }

        public bool recordExists(int count)
        {
            if (count == 0)
                return false;
            else
                return true;
        }
        
    }

}
