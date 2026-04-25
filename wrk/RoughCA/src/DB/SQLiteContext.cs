using Microsoft.Data.Sqlite;
using System.Data.SQLite;

namespace Arteria_s.DB.Base
{
	//　データベースコンテキスト
	public class SQLiteContext
	{
		SqliteConnection	m_pConnection;

		public SQLiteContext()
		{

		}

		//　
		public SQLiteContext(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt)
		{
			var pConnectionString = $"Data Source={DatabaseName}";

			m_pConnection = new SqliteConnection(pConnectionString);
			m_pConnection.Open();
		}

		//　トランザクションを開始
		public SqliteTransaction BeginTransaction()
		{
			return(m_pConnection.BeginTransaction());
		}

		//　
		~SQLiteContext()
		{
			m_pConnection.Close();
			m_pConnection = null;
		}

		//　PostgreSQL セッション変数を更新
		protected void SetSessionVariable(string VariableName, string VariableValue)
		{
			var pSQL = "SELECT SET_CONFIG(@VariableName, @VariableValue, false);";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);
				pCommand.Parameters.AddWithValue("VariableValue", VariableValue);

				try
				{
					pCommand.ExecuteNonQuery();
				}
				catch (SqliteException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.Message} Code: {e.ErrorCode}");
				}
			}

			return;
		}

		protected string GetSessionVariable(string VariableName)
		{
			string	VariableValue = null;

			var pSQL = "SELECT current_setting(@VariableName);";
			using (var pCommand = m_pConnection.CreateCommand())
			{
				pCommand.CommandText = pSQL;
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);

				try
				{
					using (var pReader = pCommand.ExecuteReader())
					{
						while (pReader.Read())
						{
							VariableValue = pReader.GetString(0);
						}
					}
				}
				catch (SqliteException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.Message} Code: {e.ErrorCode}");
				}
			}

			return (VariableValue);
		}
	}
}
