// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer.Configuration;

namespace MssqlMcp.ConfigurationUtility.Tests
{
    /// <summary>
    /// Tests for <see cref="DatabaseEditModel"/>.
    /// </summary>
    public sealed class DatabaseEditModelTests
    {
        private const string ValidConnectionString = "Server=.;Database=master;Integrated Security=True";

        private static readonly string[] ExistingKeys = ["sales-dev", "reporting"];

        [Fact]
        public void NewDatabase_IsActive_WithNoEnvironmentOrDescription()
        {
            DatabaseEditModel model = new DatabaseEditModel();

            Assert.True(model.IsActive);
            Assert.Equal(string.Empty, model.EnvironmentName);
            Assert.Equal(string.Empty, model.Description);
        }

        [Fact]
        public void GetValidationErrors_ReturnsEmpty_WhenValid()
        {
            Assert.Empty(CreateValidModel().GetValidationErrors(ExistingKeys));
        }

        [Fact]
        public void GetValidationErrors_RejectsDuplicateKey_IgnoringCase_AndNamesExistingKey()
        {
            DatabaseEditModel model = CreateValidModel();

            model.DatabaseKey = "SALES-DEV";

            string error = Assert.Single(model.GetValidationErrors(ExistingKeys));

            Assert.Contains("'sales-dev'", error, StringComparison.Ordinal);
        }

        [Fact]
        public void GetValidationErrors_AllowsOwnKey_WhenEditing()
        {
            DatabaseEditModel model = CreateValidModel();

            model.OriginalDatabaseKey = "sales-dev";
            model.DatabaseKey = "sales-dev";

            Assert.Empty(model.GetValidationErrors(ExistingKeys));
        }

        [Fact]
        public void GetValidationErrors_RequiresKeyEnvironmentDescriptionAndReadOnlyConnectionString()
        {
            List<string> errors = new DatabaseEditModel().GetValidationErrors(ExistingKeys);

            Assert.Equal(4, errors.Count);
            Assert.Contains("An environment is required", errors);
            Assert.Contains("A description is required", errors);
        }

        [Fact]
        public void GetValidationErrors_RequiresDescription_WhenBlank()
        {
            DatabaseEditModel model = CreateValidModel();

            model.Description = "   ";

            Assert.Equal("A description is required", Assert.Single(model.GetValidationErrors(ExistingKeys)));
        }

        [Fact]
        public void GetValidationErrors_RequiresEnvironment_WhenBlank()
        {
            DatabaseEditModel model = CreateValidModel();

            model.EnvironmentName = "   ";

            Assert.Equal("An environment is required", Assert.Single(model.GetValidationErrors(ExistingKeys)));
        }

        [Theory]
        [InlineData("not a connection string")]
        [InlineData("Sever=.;Database=master")]
        [InlineData("Database=master;Integrated Security=True")]
        public void GetValidationErrors_RejectsInvalidWriteConnectionString(string writeConnectionString)
        {
            DatabaseEditModel model = CreateValidModel();

            model.WriteConnectionString = writeConnectionString;

            Assert.Single(model.GetValidationErrors(ExistingKeys));
        }

        [Fact]
        public void Trim_TrimsEnvironmentAndDescription()
        {
            DatabaseEditModel model = CreateValidModel();

            model.EnvironmentName = "  Live ";
            model.Description = " Main 3sixty database  ";
            model.Trim();

            Assert.Equal("Live", model.EnvironmentName);
            Assert.Equal("Main 3sixty database", model.Description);
        }

        [Fact]
        public void FromRegistration_CopiesEnvironmentAndDescription()
        {
            DatabaseEditModel model = DatabaseEditModel.FromRegistration(new DatabaseRegistration("sales", ValidConnectionString, null, true, "Live", "Sales database"));

            Assert.Equal("Live", model.EnvironmentName);
            Assert.Equal("Sales database", model.Description);
        }

        [Fact]
        public void CloneForEdit_DropsError_AndKeepsOriginalKeyActiveStateEnvironmentAndDescription()
        {
            DatabaseEditModel model = DatabaseEditModel.FromError(new DatabaseSummary("legacy", false, "Live", "Old database"), "Could not be decrypted");

            DatabaseEditModel clone = model.CloneForEdit();

            Assert.True(model.HasError);
            Assert.False(clone.HasError);
            Assert.False(clone.IsAddMode);
            Assert.Equal("legacy", clone.OriginalDatabaseKey);
            Assert.False(clone.IsActive);
            Assert.Equal("Live", clone.EnvironmentName);
            Assert.Equal("Old database", clone.Description);
        }

        [Fact]
        public void ToRegistration_CopiesDetails_AndSavesBlankWriteConnectionStringAsNone()
        {
            DatabaseEditModel model = CreateValidModel();

            model.IsActive = false;

            DatabaseRegistration registration = model.ToRegistration();

            Assert.Equal("new-db", registration.DatabaseKey);
            Assert.Equal("Dev", registration.EnvironmentName);
            Assert.Equal("Test database", registration.Description);
            Assert.False(registration.IsActive);
            Assert.Null(registration.WriteConnectionString);
        }

        /// <summary>
        /// Creates a new database with valid details.
        /// </summary>
        /// <returns>Valid model.</returns>
        private static DatabaseEditModel CreateValidModel()
        {
            return new DatabaseEditModel
            {
                DatabaseKey = "new-db",
                EnvironmentName = "Dev",
                Description = "Test database",
                ReadOnlyConnectionString = ValidConnectionString
            };
        }
    }
}
