sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast) => {
    "use strict";

    return BaseController.extend("s4herp.ui.controller.DocumentDisplay", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ lines: [] }));
            this.getView().setModel(new JSONModel({}), "workflow");
            this.getRouter().getRoute("journalDisplay")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _path(suffix) {
            return "/api/v1/finance/journal-entries/"
                + encodeURIComponent(this._key.companyCode) + "/"
                + encodeURIComponent(this._key.fiscalYear) + "/"
                + encodeURIComponent(this._key.documentNumber)
                + (suffix || "");
        },

        _onRouteMatched(event) {
            this._key = event.getParameter("arguments");
            this._load();
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(
                    this._path(), { context: this.contextData() });
                view.getModel().setData(result);
                await this._loadWorkflow();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        /**
         * A document that was never submitted has no workflow, and the API says so
         * with 404. That is the normal case for a directly posted document, so it
         * clears the panel rather than raising an error.
         */
        async _loadWorkflow() {
            try {
                const workflow = await this.api().request(
                    this._path("/workflow"), { context: this.contextData() });
                this.getView().getModel("workflow").setData(workflow);
            } catch (error) {
                if (error.status !== 404) {
                    throw error;
                }
                this.getView().getModel("workflow").setData({});
            }
        },

        /** Every workflow action is the same shape: POST, report, reload. */
        async _act(suffix, body, successKey) {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(this._path(suffix), {
                    method: "POST",
                    body: body || {},
                    context: this.contextData()
                });
                MessageToast.show(this.text(successKey, [result.documentStatus]));
                await this._load();
            } catch (error) {
                MessageBox.error(error.message, {
                    title: this.text("requestFailed"),
                    details: error.errorCode
                });
            } finally {
                view.setBusy(false);
            }
        },

        onSubmit() {
            this._act("/submit", {}, "submittedOk");
        },

        onApprove() {
            this._promptComment("approve", "approveComment", false, (comment) =>
                this._act("/approve", { comment }, "approvedOk"));
        },

        onReject() {
            // Mandatory server-side, so the dialog will not send an empty one —
            // a round trip to be told the obvious helps nobody.
            this._promptComment("reject", "rejectComment", true, (comment) =>
                this._act("/reject", { comment }, "rejectedOk"));
        },

        onWithdraw() {
            this._promptComment("withdraw", "withdrawComment", false, (comment) =>
                this._act("/withdraw", { comment }, "withdrawnOk"));
        },

        onDiscard() {
            MessageBox.warning(this.text("discardConfirm"), {
                title: this.text("discard"),
                actions: [MessageBox.Action.DELETE, MessageBox.Action.CANCEL],
                emphasizedAction: MessageBox.Action.CANCEL,
                onClose: (action) => {
                    if (action === MessageBox.Action.DELETE) {
                        this._discard();
                    }
                }
            });
        },

        async _discard() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(this._path(), {
                    method: "DELETE",
                    context: this.contextData()
                });
                MessageToast.show(this.text("discardedOk", [result.documentNumberFormatted]));
                this.getRouter().navTo("journalCreate");
            } catch (error) {
                MessageBox.error(error.message, {
                    title: this.text("requestFailed"),
                    details: error.errorCode
                });
            } finally {
                view.setBusy(false);
            }
        },

        onReverse() {
            // A reversal reason is mandatory server-side, so ask rather than
            // sending a placeholder the auditor would later have to interpret.
            MessageBox.confirm(this.text("reverseConfirm"), {
                title: this.text("reverse"),
                onClose: (action) => {
                    if (action === MessageBox.Action.OK) {
                        this._reverse();
                    }
                }
            });
        },

        async _reverse() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const result = await this.api().request(
                    this._path("/reverse"),
                    {
                        method: "POST",
                        body: { reversalReasonCode: "01" },
                        context: this.contextData()
                    });

                MessageToast.show(this.text("reversedOk", [result.reversalDocumentNumberFormatted]));
                if (result.postingDateMoved) {
                    MessageBox.information(this.text("reversalDateMoved", [result.reversalPostingDate]));
                }
                this._load();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
