﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SyslogLogging;
using WatsonWebserver;
using DatabaseWrapper;

namespace RestDb
{
    partial class RestDbServer
    {
        static async Task GetTable(HttpContext ctx)
        {
            string dbName = ctx.Request.RawUrlEntries[0];
            string tableName = ctx.Request.RawUrlEntries[1];
            int idVal = 0;
            if (ctx.Request.RawUrlEntries.Count == 3) Int32.TryParse(ctx.Request.RawUrlEntries[2], out idVal);

            Table currTable = _Databases.GetTableByName(dbName, tableName);
            if (currTable == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }
             
            DatabaseClient db = _Databases.GetDatabaseClient(dbName);
            if (db == null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Not found", null), true));
                return;
            }

            #region Check-for-Describe

            if (ctx.Request.QuerystringEntries.ContainsKey("_describe"))
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(currTable, true));
                return;
            }

            #endregion
            
            #region Retrieve-Objects

            DataTable result = null;

            int? indexStart = null;
            int? maxResults = null;
            string orderBy = null;
            List<string> returnFields = null;
            Expression filter = null; 

            if (idVal > 0)
            {
                if (String.IsNullOrEmpty(currTable.PrimaryKey))
                {
                    _Logging.Warn("GetTable no primary key defined for table " + tableName + " in database " + dbName);
                    ctx.Response.StatusCode = 400;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send(Common.SerializeJson(new ErrorResponse("Bad request", "No primary key for table " + tableName), true));
                    return;
                }

                filter = new Expression(currTable.PrimaryKey, Operators.Equals, idVal);
            }

            if (ctx.Request.QuerystringEntries != null && ctx.Request.QuerystringEntries.Count > 0)
            {
                foreach (KeyValuePair<string, string> currKvp in ctx.Request.QuerystringEntries)
                {
                    if (_ControlQueryKeys.Contains(currKvp.Key)) continue;
                    if (filter == null) filter = new Expression(currKvp.Key, Operators.Equals, currKvp.Value);
                    else filter = Expression.PrependAndClause(
                        new Expression(currKvp.Key, Operators.Equals, currKvp.Value),
                        filter);
                }
            }

            if (ctx.Request.QuerystringEntries.ContainsKey("_index_start")) indexStart = Convert.ToInt32(ctx.Request.QuerystringEntries["_index_start"]);
            if (ctx.Request.QuerystringEntries.ContainsKey("_max_results")) maxResults = Convert.ToInt32(ctx.Request.QuerystringEntries["_max_results"]);
            if (ctx.Request.QuerystringEntries.ContainsKey("_order_by")) orderBy = WebUtility.UrlDecode(ctx.Request.QuerystringEntries["_order_by"]); 
            if (ctx.Request.QuerystringEntries.ContainsKey("_return_fields")) returnFields = Common.CsvToStringList(ctx.Request.QuerystringEntries["_return_fields"]);

            result = db.Select(tableName, indexStart, maxResults, returnFields, filter, orderBy);

            if (result == null || result.Rows.Count < 1)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(new List<dynamic>(), true));
                return;
            }
            else
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(Common.SerializeJson(Common.DataTableToListDynamic(result), true));
                return;
            }

            #endregion 
        }
    }
}
