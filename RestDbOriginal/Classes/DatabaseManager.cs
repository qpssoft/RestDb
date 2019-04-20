﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyslogLogging;
using DatabaseWrapper;

namespace RestDb
{
    public class DatabaseManager
    {
        #region Constructors-and-Factories

        public DatabaseManager(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Settings = settings;
            _Logging = logging;
            _Databases = new Dictionary<string, DatabaseClient>();
            _DatabasesLock = new object();

            InitializeDatabases();
        }

        #endregion

        #region Public-Members

        #endregion

        #region Private-Members

        private Settings _Settings;
        private LoggingModule _Logging;
        private Dictionary<string, DatabaseClient> _Databases;
        private readonly object _DatabasesLock;

        #endregion

        #region Public-Methods
        
        public List<string> ListDatabasesByName()
        {
            List<string> ret = new List<string>();

            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseClient> curr in _Databases)
                {
                    ret.Add(curr.Key);
                }

                return ret;
            }
        }

        public Database GetDatabaseByName(string dbName)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            return _Settings.GetDatabaseByName(dbName);
        }

        public List<Table> GetTables(string dbName, bool describe)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            
            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTables unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = db.ListTables();
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTables no tables returned from list tables for database " + dbName);
                return null;
            }
            else
            {
                _Logging.Log(LoggingModule.Severity.Debug, "GetTables returning " + tableNames.Count + " tables for database " + dbName);
            }

            List<Table> ret = new List<Table>();

            foreach (string curr in tableNames)
            {
                Table currTable = new Table();
                currTable.Name = curr;
                
                if (describe)
                {
                    currTable.Columns = new List<Column>();
                    List<DatabaseWrapper.Column> columns = db.DescribeTable(curr);
                    if (columns == null || columns.Count < 1)
                    {
                        _Logging.Log(LoggingModule.Severity.Warn, "GetTables no columns found for table " + curr + " in database " + dbName);
                        ret.Add(currTable);
                        continue;
                    }

                    foreach (DatabaseWrapper.Column currColumn in columns)
                    {
                        Column tempColumn = new Column();
                        tempColumn.Name = currColumn.Name;
                        tempColumn.Nullable = currColumn.Nullable;
                        tempColumn.MaxLength = currColumn.MaxLength;
                        tempColumn.Type = currColumn.DataType;
                        if (currColumn.IsPrimaryKey) currTable.PrimaryKey = tempColumn.Name;

                        currTable.Columns.Add(tempColumn);
                        // _Logging.Log(LoggingModule.Severity.Debug, "GetTables adding column " + tempColumn.Name + " for table " + currTable.Name + " database " + dbName);
                    }
                }

                ret.Add(currTable);
            }

            return ret;
        }

        public List<string> GetTableNames(string dbName)
        {            
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
             
            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTableNames unable to find client for database " + dbName);
                return null;
            }

            List<string> tableNames = db.ListTables();
            if (tableNames == null || tableNames.Count < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTableNames no tables returned from list tables for database " + dbName);
                return null;
            }

            return tableNames;
        }

        public Table GetTableByName(string dbName, string tableName)
        {
            if (String.IsNullOrEmpty(dbName)) throw new ArgumentNullException(nameof(dbName));
            if (String.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            DatabaseClient db = GetDatabaseClient(dbName);
            if (db == null)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTableByName unable to find client for database " + dbName);
                return null;
            }

            Table ret = new Table();
            ret.Name = tableName;
            ret.Columns = new List<Column>();

            List<DatabaseWrapper.Column> columns = db.DescribeTable(tableName);
            if (columns == null || columns.Count < 1)
            {
                _Logging.Log(LoggingModule.Severity.Warn, "GetTableByName no columns found for table " + tableName + " in database " + dbName);
                return ret;
            }

            foreach (DatabaseWrapper.Column currColumn in columns)
            {
                Column tempColumn = new Column();
                tempColumn.Name = currColumn.Name;
                tempColumn.Nullable = currColumn.Nullable;
                tempColumn.MaxLength = currColumn.MaxLength; 
                tempColumn.Type = currColumn.DataType;
                if (currColumn.IsPrimaryKey) ret.PrimaryKey = tempColumn.Name;

                ret.Columns.Add(tempColumn);
            }

            return ret;
        }

        public DatabaseClient GetDatabaseClient(string dbName)
        {
            lock (_DatabasesLock)
            {
                foreach (KeyValuePair<string, DatabaseClient> curr in _Databases)
                {
                    if (curr.Key.ToLower().Equals(dbName.ToLower())) return curr.Value;
                }

                return null;
            }
        }

        #endregion

        #region Private-Methods

        private void InitializeDatabases()
        {
            _Databases = new Dictionary<string, DatabaseClient>();

            foreach (Database curr in _Settings.Databases)
            {
                _Logging.Log(LoggingModule.Severity.Debug, "InitializeDatabases initializing db " + curr.ToString());

                DatabaseClient db = new DatabaseClient(
                    curr.Type,
                    curr.Hostname,
                    curr.Port,
                    curr.Username,
                    curr.Password,
                    curr.Instance,
                    curr.Name);
                
                if (curr.Debug)
                {
                    _Logging.Log(LoggingModule.Severity.Debug, "InitializeDatabases enabling debug for db " + curr.Name);
                    db.DebugRawQuery = true;
                    db.DebugResultRowCount = true;
                }

                _Databases.Add(curr.Name, db);
            }
        }

        #endregion

        #region Public-Static-Methods

        #endregion

        #region Private-Static-Methods

        #endregion
    }
}
