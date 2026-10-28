// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace MssqlMcp.ConfigurationUtility.Tests
{
    /// <summary>
    /// Tests for <see cref="ConnectionStringBuilderModel"/>.
    /// </summary>
    public sealed class ConnectionStringBuilderModelTests
    {
        [Fact]
        public void NewConnectionString_DefaultsToWindowsAuthentication()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel(null)
            {
                Server = ".",
                Database = "master"
            };

            SqlConnectionStringBuilder result = new SqlConnectionStringBuilder(model.ConnectionString);

            Assert.True(model.IsWindowsAuthentication);
            Assert.Equal(".", result.DataSource);
            Assert.Equal("master", result.InitialCatalog);
            Assert.True(result.IntegratedSecurity);
            Assert.False(result.ShouldSerialize("User ID"));
            Assert.False(result.ShouldSerialize("Trust Server Certificate"));
        }

        [Fact]
        public void SqlServerAuthentication_SetsCredentials_AndRemovesIntegratedSecurity()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=.;Database=master;Integrated Security=True")
            {
                IsSqlServerAuthentication = true,
                UserName = " reader ",
                Password = "Secret;1"
            };

            SqlConnectionStringBuilder result = new SqlConnectionStringBuilder(model.ConnectionString);

            Assert.False(result.ShouldSerialize("Integrated Security"));
            Assert.Equal("reader", result.UserID);
            Assert.Equal("Secret;1", result.Password);
        }

        [Fact]
        public void WindowsAuthentication_RemovesCredentials()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=.;Database=master;User ID=reader;Password=Secret")
            {
                IsWindowsAuthentication = true
            };

            SqlConnectionStringBuilder result = new SqlConnectionStringBuilder(model.ConnectionString);

            Assert.True(result.IntegratedSecurity);
            Assert.False(result.ShouldSerialize("User ID"));
            Assert.False(result.ShouldSerialize("Password"));
        }

        [Fact]
        public void ExistingConnectionString_IsParsed()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=tcp:db.example.com,1433;Database=Sales;User ID=reader;Password=Secret;TrustServerCertificate=True");

            Assert.Equal("tcp:db.example.com,1433", model.Server);
            Assert.Equal("Sales", model.Database);
            Assert.True(model.IsSqlServerAuthentication);
            Assert.Equal("reader", model.UserName);
            Assert.Equal("Secret", model.Password);
            Assert.True(model.TrustServerCertificate);
            Assert.False(model.HasOtherAuthentication);
        }

        [Fact]
        public void OtherSettings_AreKept()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=.;Database=master;Integrated Security=True;Encrypt=Strict;Connect Timeout=45")
            {
                Server = "other",
                IsSqlServerAuthentication = true,
                UserName = "reader"
            };

            SqlConnectionStringBuilder result = new SqlConnectionStringBuilder(model.ConnectionString);

            Assert.Equal("other", result.DataSource);
            Assert.Equal(SqlConnectionEncryptOption.Strict, result.Encrypt);
            Assert.Equal(45, result.ConnectTimeout);
        }

        [Fact]
        public void EntraAuthentication_IsKept_UntilAnotherModeIsChosen()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=tcp:db.database.windows.net;Database=Sales;Authentication=Active Directory Interactive;User ID=someone@example.com");

            Assert.True(model.IsOtherAuthentication);
            Assert.True(model.HasOtherAuthentication);
            Assert.False(model.IsCredentialsEnabled);
            Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryInteractive, new SqlConnectionStringBuilder(model.ConnectionString).Authentication);

            model.IsWindowsAuthentication = true;

            SqlConnectionStringBuilder result = new SqlConnectionStringBuilder(model.ConnectionString);

            Assert.Equal(SqlAuthenticationMethod.NotSpecified, result.Authentication);
            Assert.False(result.ShouldSerialize("User ID"));
            Assert.True(result.IntegratedSecurity);
        }

        [Fact]
        public void OtherAuthentication_CannotBeChosen_WhenOriginalDidNotUseIt()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=.;Database=master;Integrated Security=True")
            {
                IsOtherAuthentication = true
            };

            Assert.True(model.IsWindowsAuthentication);
        }

        [Fact]
        public void ClearingTrustServerCertificate_RemovesKeyword()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True")
            {
                TrustServerCertificate = false
            };

            Assert.False(new SqlConnectionStringBuilder(model.ConnectionString).ShouldSerialize("Trust Server Certificate"));
        }

        [Fact]
        public void InvalidConnectionString_StartsFromScratch()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel("not a connection string");

            Assert.Equal(string.Empty, model.Server);
            Assert.True(model.IsWindowsAuthentication);
        }

        [Fact]
        public void ChangingSetting_RaisesConnectionStringChanged()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel(null);
            List<string?> changedProperties = new List<string?>();

            model.PropertyChanged += (sender, e) => changedProperties.Add(e.PropertyName);
            model.IsSqlServerAuthentication = true;

            Assert.Contains(nameof(ConnectionStringBuilderModel.ConnectionString), changedProperties);
            Assert.Contains(nameof(ConnectionStringBuilderModel.IsCredentialsEnabled), changedProperties);
            Assert.Contains(nameof(ConnectionStringBuilderModel.IsWindowsAuthentication), changedProperties);
        }

        [Fact]
        public void GetValidationErrors_RequiresServerDatabaseAndUserName()
        {
            ConnectionStringBuilderModel model = new ConnectionStringBuilderModel(null)
            {
                IsSqlServerAuthentication = true
            };

            List<string> errors = model.GetValidationErrors();

            Assert.Equal(3, errors.Count);

            model.Server = ".";
            model.Database = "master";
            model.UserName = "reader";

            Assert.Empty(model.GetValidationErrors());
        }
    }
}
