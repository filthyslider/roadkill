var Roadkill;
(function (Roadkill) {
    (function (Site) {
        /// <reference path="typescript-ref/references.ts" />
        (function (Admin) {
            var Settings = (function () {
                function Settings() {
                    var _this = this;
                    // Help popovers
                    $("input[rel=popover][type!=checkbox]").popover({ container: "body", placement: "right", trigger: "hover", html: true });
                    $("input[type=checkbox][rel=popover],textarea[rel=popover],select[rel=popover]").popover({ container: "body", placement: "right", trigger: "hover", html: true });

                    // Make the windows auth checkbox toggle the forms-auth/windows-auth sections.
                    $("#UseWindowsAuth").click(function () {
                        this.toggleUserSettings();
                    });

                    // Button clicks
                    $("#testdbconnection").click(function (e) {
                        _this.OnTestDatabaseClick();
                    });
                    $("#testattachments").click(function (e) {
                        _this.OnTestAttachmentsClick();
                    });

                    // Form validation
                    var validationRules = {
                        AllowedFileTypes: {
                            required: true
                        },
                        AttachmentsFolder: {
                            required: true
                        }
                    };
                    var validation = new Roadkill.Site.Validation();
                    validation.Configure("#settings-form", validationRules);
                }
                Settings.prototype.OnTestDatabaseClick = function () {
                    $("#db-loading").removeClass("hidden");
                    $("#db-loading").show();

                    var jsonData = {
                        "connectionString": $("#ConnectionString").val(),
                        "databaseType": $("#DataStoreTypeName").val()
                    };

                    this.makeAjaxRequest(ROADKILL_TESTDB_URL, jsonData, "Something went wrong", this.TestDatabaseSuccess);
                };

                Settings.prototype.TestDatabaseSuccess = function (data) {
                    // TODO-translation
                    $("#db-loading").hide();
                    if (data.Success) {
                        toastr.success("Database connection was successful.");
                    } else {
                        toastr.error("Database connection failed: <br/>" + data.ErrorMessage);
                    }
                };

                Settings.prototype.OnTestAttachmentsClick = function () {
                    var jsonData = {
                        "folder": $("#AttachmentsFolder").val()
                    };

                    this.makeAjaxRequest(ROADKILL_TESTATTACHMENTS_URL, jsonData, "Something went wrong", this.TestAttachmentsSuccess);
                };

                Settings.prototype.TestAttachmentsSuccess = function (data) {
                    if (data.Success) {
                        toastr.success("Success! The directory exists and can be written to.");
                    } else {
                        toastr.error("Attachments directory failed: <br/>" + data.ErrorMessage);
                    }
                };

                Settings.prototype.makeAjaxRequest = function (url, data, errorMessage, successFunction) {
                    var request = $.ajax({
                        type: "GET",
                        url: url,
                        data: data,
                        dataType: "json"
                    });

                    request.done(successFunction);

                    request.fail(function (jqXHR, textStatus, errorThrown) {
                        if (errorThrown.message.indexOf("unexpected character") !== -1) {
                            window.location = window.location;
                        } else {
                            toastr.error(errorMessage + errorThrown);
                        }
                    });
                };

                Settings.prototype.ToggleUserSettings = function () {
                    if ($("#UseWindowsAuth").is(":checked")) {
                        $("#aspnetuser-settings").hide();
                        $("#ldapsettings").show();
                        $("#ldapsettings").removeClass("hidden");
                    } else {
                        $("#ldapsettings").hide();
                        $("#aspnetuser-settings").show();
                        $("#aspnetuser-settings").removeClass("hidden");
                    }
                };
                return Settings;
            })();
            Admin.Settings = Settings;
        })(Site.Admin || (Site.Admin = {}));
        var Admin = Site.Admin;
    })(Roadkill.Site || (Roadkill.Site = {}));
    var Site = Roadkill.Site;
})(Roadkill || (Roadkill = {}));
//# sourceMappingURL=settings.js.map