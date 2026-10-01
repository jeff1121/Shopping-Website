using System;
using System.Data;
using System.Web.SessionState;

namespace Website.Data
{
    /// <summary>
    /// Session["count"] 購物車 DataTable 的共用操作。
    /// 欄位固定為 sno, pimage, pname, price, quantity, total, uname（uname 為賣家姓名，對應 violet_cart.sname）。
    /// </summary>
    public static class CartSession
    {
        /// <summary>Session 中存放購物車 DataTable 的鍵。</summary>
        public const string Key = "count";

        /// <summary>
        /// 建立欄位齊全的空購物車 DataTable。
        /// </summary>
        /// <returns>空的購物車表。</returns>
        public static DataTable CreateTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("sno");
            table.Columns.Add("pimage");
            table.Columns.Add("pname");
            table.Columns.Add("price");
            table.Columns.Add("quantity");
            table.Columns.Add("total");
            table.Columns.Add("uname");
            return table;
        }

        /// <summary>
        /// 取得目前的購物車 DataTable。
        /// </summary>
        /// <param name="session">目前請求的 Session。</param>
        /// <returns>購物車表；尚未建立時為 null。</returns>
        public static DataTable Get(HttpSessionState session)
        {
            return session[Key] as DataTable;
        }

        /// <summary>
        /// 取得購物車品項數，供頁首購物車徽章顯示。
        /// </summary>
        /// <param name="session">目前請求的 Session。</param>
        /// <returns>品項數；購物車不存在時為 0。</returns>
        public static int Count(HttpSessionState session)
        {
            DataTable table = Get(session);
            return table == null ? 0 : table.Rows.Count;
        }

        /// <summary>
        /// 計算購物車所有品項小計（total）的總和。
        /// </summary>
        /// <param name="table">購物車表。</param>
        /// <returns>總金額；購物車不存在時為 0。</returns>
        public static decimal GrandTotal(DataTable table)
        {
            decimal sum = 0;
            if (table != null)
            {
                foreach (DataRow row in table.Rows)
                {
                    sum += Convert.ToDecimal(row["total"]);
                }
            }

            return sum;
        }

        /// <summary>
        /// 從 violet_cart 讀出會員已儲存的購物車，依 sno 排序並重新編號為 1..N，存入 Session["count"]。
        /// 登入成功時呼叫。
        /// </summary>
        /// <param name="session">目前請求的 Session。</param>
        /// <param name="uname">會員姓名（violet_cart.uname）。</param>
        public static void LoadFromDatabase(HttpSessionState session, string uname)
        {
            DataTable table = CreateTable();
            DataTable saved = Db.Query("SELECT pimage, pname, price, quantity, sname FROM violet_cart WHERE uname=@uname ORDER BY sno", Db.Param("@uname", uname));
            int sno = 1;
            foreach (DataRow source in saved.Rows)
            {
                DataRow row = table.NewRow();
                row["sno"] = sno++;
                row["pimage"] = source["pimage"].ToString();
                row["pname"] = source["pname"].ToString();
                row["price"] = source["price"].ToString();
                row["quantity"] = source["quantity"].ToString();
                row["total"] = Convert.ToDecimal(source["price"]) * Convert.ToInt32(source["quantity"]);
                row["uname"] = source["sname"].ToString();
                table.Rows.Add(row);
            }

            session[Key] = table;
        }
    }
}
