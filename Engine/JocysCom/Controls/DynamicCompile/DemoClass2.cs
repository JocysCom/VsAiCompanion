//using System;
//using System.Linq;
////using Xenial.XeniZen.Engine.Ticks.Data;
//using System.Collections.Generic;

//namespace JocysCom.ClassLibrary.Controls.DynamicCompile
//{

//    class DemoClass2
//    {
//        public static string Main()
//        {

//            //TickClassesDataContext db = TickClassesDataContext.Current;
//            //IQueryable<Tick> query = (from row in db.Ticks where row.TickGuid == Guid.Empty select row).Skip(80).Take(20);
//            //int rowsSkip = 0;
//            //int rowsTake = 10;
//            //int rowsCount = 59;
//            //int pageCount = 0;
//            //int pageIndex = 0;
//            //return db.GetCommand(query).CommandText;

//            //System.Data.Linq.SqlClient.Sql2008Provider sp;
//            //Guid id = new Guid("00000000-0000-0000-0000-000000000001");

//            //IQueryable<Guid> query = (
//            //    from row in db.Ticks where row.ActionName == "Dedupe" && (row.ItemId == id || row.RelatedItemId == id) select row.ItemId
//            //    ).Union( from row in db.Ticks where row.ActionName == "Dedupe" && row.ItemId == id select row.RelatedItemId).Distinct();
//            //IQueryable<Guid> query2 = from row in db.Ticks where row.ActionName = "Dedupe"


//            //SELECT TOP 1 BookId, Count(*) FROM (
//            //    -- Select all Books by these two authors.
//            //    SELECT BookId, AuthorId
//            //    FROM xefba_db_Books_Authors
//            //    WHERE AuthorId = 'AC8A3DFF-16E2-427A-8968-1E1276AAEE17'
//            //    OR AuthorId = '9DCB6CAF-2F9D-4D26-9F14-629738DF8C0F'
//            //    GROUP BY BookId, AuthorId
//            //) t1
//            //GROUP BY BookId
//            //HAVING Count(*) > 1
//            //IQueryable<Engine.Books.Data.BooksAuthor> query = 
//            //IQueryable query = 
//            //(from row
//            //in db.BooksAuthors
//            //             where row.AuthorId == new Guid("AC8A3DFF-16E2-427A-8968-1E1276AAEE17")
//            //|| row.AuthorId == new Guid("9DCB6CAF-2F9D-4D26-9F14-629738DF8C0F"));
//            //// group row by row.BookId into g
//            ////select new { key = g.Key, G1 = g };
//            ////IQueryable query2 = query


		


//            //int lastSize = rowsCount % rowsTake;
//            //pageCount = (rowsCount - lastSize) / rowsTake + Math.Min(1, lastSize);

//            //return pageCount.ToString();

//            System.Data.SqlClient.SqlParameter p = new System.Data.SqlClient.SqlParameter();
//            //p.SqlDbType = System.Data.SqlDbType.Udt;
//            //p.UdtTypeName = "Char(50)";
//            //p.Value = "null";
//            //p.ResetSqlDbType();


//            return p.SqlDbType.ToString() ;
			
//        }

//        public static string Main2()
//        {
//            return "Hello World!";
//        }

//        public static string Main3()
//        {
//            //string s = "Dupe";
//            //Engine.Books.Data.BookClassesDataContext db = Engine.Books.Data.BookClassesDataContext.Current;
//            //IEnumerable<Engine.Books.Data.Book> query = db.ExecuteQuery<Engine.Books.Data.Book>(@"SELECT * FROM [dbo].[xefba_db_Books] WHERE CONTAINS(Title, {0})", s);
//            //IEnumerable<Engine.Books.Data.Book> query2 = query.Where(x=>x.Title == "Dups");
//            ////IEnumerable<Engine.Books.Data.Book> query2 = from row in query.AsQueryable() where row.Title== "Dup" select row;
//            //int r = 0;
//            //query2.ToList();
//            ////foreach (Engine.Books.Data.Book b in query2)
//            ////{
//            ////	r++;
//            ////}
//            ////return db.Connection.ConnectionString;
//            ////db.GetCommand(
//            //return r.ToString()+": "+db.GetCommand(query.AsQueryable()).CommandText;
//            return "Main 3 results";
//        }


		

//    }
//}



