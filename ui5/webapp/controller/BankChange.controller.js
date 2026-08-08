sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox",
    "sap/m/MessageToast"
], (BaseController, JSONModel, formatter, MessageBox, MessageToast) => {
    "use strict";

    /**
     * The eight fields of a bank record, in the order an approver reads them.
     * Held as a list rather than generated from the payload so the comparison
     * always shows every field — including the ones that did not change, because
     * "the account holder is unchanged" is part of what makes a new account
     * number believable.
     */
    const FIELDS = [
        { key: "countryCode", label: "bankFieldCountry" },
        { key: "bankKey", label: "bankFieldBankKey" },
        { key: "bankName", label: "bankFieldBankName" },
        { key: "accountNumber", label: "bankFieldAccountNumber" },
        { key: "accountHolder", label: "bankFieldAccountHolder" },
        { key: "iban", label: "bankFieldIban" },
        { key: "swift", label: "bankFieldSwift" },
        { key: "isDefault", label: "bankFieldIsDefault" }
    ];

    /**
     * One pending change to a partner's bank details, and the decision on it.
     *
     * The screen exists because the inbox row cannot carry the comparison: an
     * approver is being asked whether *this* account should replace *that* one,
     * and the answer depends on seeing both.
     */
    return BaseController.extend("s4herp.ui.controller.BankChange", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ fields: [], approvalSteps: [] }));
            this.getRouter().getRoute("bankChange")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _onRouteMatched(event) {
            this._requestId = event.getParameter("arguments").requestId;
            this._load();
        },

        _path(suffix) {
            return "/api/v1/business-partners/bank-details/changes/"
                + encodeURIComponent(this._requestId) + (suffix || "");
        },

        async _load() {
            const view = this.getView();
            view.setBusy(true);
            try {
                const request = await this.api().request(
                    this._path(), { context: this.contextData() });

                view.getModel().setData({
                    ...request,
                    fields: this._compare(request.previous, request.proposed)
                });
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        /**
         * Before and after, per field, with a flag on the ones that moved. A
         * create has no before, and every field is then a change — which is the
         * truth, and showing eight highlighted rows for a new account is right.
         */
        _compare(previous, proposed) {
            return FIELDS.map((field) => {
                const before = this._display(previous ? previous[field.key] : null);
                const after = this._display(proposed ? proposed[field.key] : null);
                return {
                    label: this.text(field.label),
                    before,
                    after,
                    changed: before !== after
                };
            });
        },

        _display(value) {
            if (value === null || value === undefined || value === "") {
                return this.text("bankFieldEmpty");
            }
            if (value === true) {
                return this.text("yes");
            }
            if (value === false) {
                return this.text("no");
            }
            return String(value);
        },

        onApprove() {
            this._promptComment("approve", "approveComment", false, (comment) =>
                this._decide("/approve", { comment }, "bankChangeApproved"));
        },

        onReject() {
            // Mandatory server-side. Asking here saves a round trip to be told
            // something the approver already knows.
            this._promptComment("reject", "rejectComment", true, (comment) =>
                this._decide("/reject", { comment }, "bankChangeRejected"));
        },

        async _decide(suffix, body, successKey) {
            const view = this.getView();
            view.setBusy(true);
            try {
                await this.api().request(this._path(suffix), {
                    method: "POST",
                    body,
                    context: this.contextData()
                });
                MessageToast.show(this.text(successKey));
                await this._load();
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        }
    });
});
