using Npgsql;

namespace Arteria_s.DB.Base
{
	//　データベースコンテキスト
	public class SQLContext
	{
		public NpgsqlConnection m_pConnection;

		public SQLContext()
		{
			m_pConnection = null;
		}

		//　
		public SQLContext(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt)
		{
			var pBuilder = new NpgsqlConnectionStringBuilder();
			pBuilder.Host = DatabaseServer;
			pBuilder.Database = DatabaseName;
			pBuilder.Username = SchemaName;
			//pBuilder.SslMode = SslMode.VerifyFull;
			pBuilder.SslMode = SslMode.Disable;
			pBuilder.SslCertificate = ClientCrt;
			pBuilder.SslKey = ClientKey;
			pBuilder.RootCertificate = TrustCrt;
			var pConnectionString = pBuilder.ConnectionString;

			m_pConnection = new NpgsqlConnection(pConnectionString);
			m_pConnection.Open();
		}

		//　トランザクションを開始
		public NpgsqlTransaction BeginTransaction()
		{
			return(m_pConnection.BeginTransaction());
		}

		//　
		~SQLContext()
		{
			m_pConnection.Close();
			m_pConnection = null;
		}

		//　PostgreSQL セッション変数を更新
		protected void SetSessionVariable(string VariableName, string VariableValue)
		{
			var pSQL = "SELECT SET_CONFIG(@VariableName, @VariableValue, false);";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
				pCommand.Parameters.Clear();
				pCommand.Parameters.AddWithValue("VariableName", VariableName);
				pCommand.Parameters.AddWithValue("VariableValue", VariableValue);

				try
				{
					pCommand.ExecuteNonQuery();
				}
				catch (PostgresException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.MessageText} Code: {e.ErrorCode}");
				}
			}

			return;
		}

		protected string GetSessionVariable(string VariableName)
		{
			string	VariableValue = null;

			var pSQL = "SELECT current_setting(@VariableName);";
			using (var pCommand = new NpgsqlCommand(pSQL, m_pConnection))
			{
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
				catch (PostgresException e)
				{
					System.Diagnostics.Debug.WriteLine($"Mesage: {e.MessageText} Code: {e.ErrorCode}");
				}
			}

			return (VariableValue);
		}
	}

	public class SQLContextEx : SQLContext
	{
		public SQLContextEx(string DatabaseServer, string DatabaseName, string SchemaName, string ClientKey, string ClientCrt, string TrustCrt, string AccountName, string AccessToken) : base(DatabaseServer, DatabaseName, SchemaName, ClientKey, ClientCrt, TrustCrt)
		{
			SetSessionVariable("app.AccountName", AccountName);
			SetSessionVariable("app.AccessToken", AccessToken);
			GetSessionVariable("app.AccountName");
		}
	}
}
