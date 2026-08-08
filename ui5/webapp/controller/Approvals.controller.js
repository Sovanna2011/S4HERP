sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageBox"
], (BaseController, JSONModel, formatter, MessageBox) => {
    "use strict";

    /**
     * Where a given kind of approvable object is decided. The map is the single
     * statement of what the inbox can open: the row's appearance and the press
     * handler both read it, so a row cannot offer navigation the controller then
     * refuses. `null` means no screen exists yet, and the row says so by being
     * inactive rather than by doing nothing when clicked.
     */
    const DESTINATIONS = {
        JournalEntry: "journalDisplay",
        PartnerBank: "bankChange",
        PaymentRun: "paymentRun"
    };

    /**
     * The approver's inbox, for every kind of object rather than journal entries
     * alone. Deliberately thin: it lists what the server says is waiting and
     * navigates to the thing itself, where the decision is taken with the detail
     * in front of you. Approving from a list is how rubber-stamping happens.
     */
    return BaseController.extend("s4herp.ui.controller.Approvals", {
        formatter,

        onInit() {
            this.getView().setModel(new JSONModel({ items: [], filter: "" }));
            this.getRouter().getRoute("approvals")
                .attachPatternMatched(this._load, this);
        },

        async _load() {
            const view = this.getView();
            const model = view.getModel();
            const filter = model.getProperty("/filter") || "";
            view.setBusy(true);
            try {
                const query = filter ? `?objectType=${encodeURIComponent(filter)}` : "";
                const items = await this.api().request(
                    `/api/v1/approvals${query}`, { context: this.contextData() });

                // The kind label and navigability are resolved here rather than in
                // a formatter: formatters have no dependable access to the resource
                // bundle, and an English-only label would quietly defeat the Khmer
                // translation the rest of the app has.
                model.setProperty("/items", (items || []).map((item) => ({
                    ...item,
                    kindText: this._kind(item.objectType),
                    navigable: Boolean(DESTINATIONS[item.objectType])
                })));
            } catch (error) {
                MessageBox.error(error.message, { title: this.text("requestFailed") });
            } finally {
                view.setBusy(false);
            }
        },

        _kind(objectType) {
            switch (objectType) {
                case "JournalEntry": return this.text("approvalKindJournalEntry");
                case "PaymentRun": return this.text("approvalKindPaymentRun");
                case "PartnerBank": return this.text("approvalKindPartnerBank");
                // An object type the front end has not been taught about still
                // appears, under its raw name. Hiding it would hide work.
                default: return objectType;
            }
        },

        onFilter(event) {
            // Read from the event rather than the model: selectedKey's two-way
            // binding has not necessarily propagated when selectionChange fires,
            // so reloading from the model reloaded the *previous* filter and the
            // list appeared not to filter at all.
            this.getView().getModel().setProperty(
                "/filter", event.getParameter("item").getKey());
            this._load();
        },

        onRefresh() {
            this._load();
        },

        onOpen(event) {
            const item = event.getSource().getBindingContext().getObject();
            const route = DESTINATIONS[item.objectType];

            if (!route) {
                MessageBox.information(
                    this.text("approvalsNoScreen", [item.kindText]),
                    { title: item.title });
                return;
            }

            if (item.objectType === "PartnerBank") {
                this.getRouter().navTo("bankChange", { requestId: item.objectId });
                return;
            }

            if (item.objectType === "PaymentRun") {
                this.getRouter().navTo("paymentRun", { runId: item.objectId });
                return;
            }

            // The inbox carries the formatted document id — KSS-1000-2026-SA-
            // 0100000042 — because that is what Workflow stores as the object key.
            // The route needs the three parts of the business key back.
            const parts = /^KSS-(\d+)-(\d+)-[A-Z0-9]+-(\d+)$/.exec(item.objectId);
            if (!parts) {
                MessageBox.error(this.text("approvalsUnparseableId", [item.objectId]));
                return;
            }

            this.getRouter().navTo("journalDisplay", {
                companyCode: parts[1],
                fiscalYear: parts[2],
                documentNumber: Number(parts[3])
            });
        }
    });
});
