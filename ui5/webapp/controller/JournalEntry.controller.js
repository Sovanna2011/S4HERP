sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/formatter",
    "sap/m/MessageToast",
    "sap/m/MessagePopover",
    "sap/m/MessageItem",
    "sap/ui/core/Messaging"
], (BaseController, JSONModel, formatter, MessageToast, MessagePopover, MessageItem, Messaging) => {
    "use strict";

    return BaseController.extend("s4herp.ui.controller.JournalEntry", {
        formatter,

        onInit() {
            this.getRouter().getRoute("journalCreate")
                .attachPatternMatched(this._onRouteMatched, this);
        },

        _onRouteMatched() {
            this.getView().setModel(new JSONModel(this._emptyDocument()));
            Messaging.removeAllMessages();
            this._recalculate();
        },

        _emptyDocument() {
            const context = this.contextData();
            const today = new Date().toISOString().slice(0, 10);
            return {
                header: {
                    companyCode: context.companyCode,
                    documentType: "SA",
                    currency: "USD",
                    documentDate: today,
                    postingDate: today,
                    reference: "",
                    headerText: ""
                },
                lines: [
                    { postingKey: "40", glAccount: "", costCenter: "", taxCode: "", amount: "0.00", lineText: "" },
                    { postingKey: "50", glAccount: "", costCenter: "", taxCode: "", amount: "0.00", lineText: "" }
                ],
                totals: { debit: 0, credit: 0, difference: 0 },
                simulation: { visible: false, lines: [] },
                messageCount: 0
            };
        },

        onAddLine() {
            const model = this.getView().getModel();
            const lines = model.getProperty("/lines");
            lines.push({ postingKey: "40", glAccount: "", costCenter: "", taxCode: "", amount: "0.00", lineText: "" });
            model.setProperty("/lines", lines.slice());
        },

        onRemoveLine() {
            const table = this.byId("lineTable");
            const selected = table.getSelectedItems();
            if (!selected.length) {
                MessageToast.show(this.text("selectLineFirst"));
                return;
            }

            const model = this.getView().getModel();
            const indices = selected
                .map((item) => Number(item.getBindingContext().getPath().split("/").pop()))
                .sort((a, b) => b - a);
            const lines = model.getProperty("/lines");
            indices.forEach((i) => lines.splice(i, 1));
            model.setProperty("/lines", lines.slice());
            table.removeSelections(true);
            this._recalculate();
        },

        /**
         * Client-side totals are an affordance only. The server recomputes and
         * rejects an unbalanced document regardless of what this says.
         */
        _recalculate() {
            const model = this.getView().getModel();
            const lines = model.getProperty("/lines") || [];
            let debit = 0;
            let credit = 0;

            lines.forEach((line) => {
                const value = Math.abs(Number(line.amount) || 0);
                if (line.postingKey === "40") {
                    debit += value;
                } else {
                    credit += value;
                }
            });

            model.setProperty("/totals", {
                debit, credit, difference: Number((debit - credit).toFixed(4))
            });
        },

        _payload() {
            const data = this.getView().getModel().getData();
            return {
                companyCode: data.header.companyCode,
                documentType: data.header.documentType,
                documentDate: data.header.documentDate,
                postingDate: data.header.postingDate,
                currency: data.header.currency,
                reference: data.header.reference || null,
                headerText: data.header.headerText || null,
                lines: data.lines
                    .filter((l) => l.glAccount)
                    .map((l) => ({
                        postingKey: l.postingKey,
                        amount: Number(l.amount) || 0,
                        glAccount: l.glAccount,
                        costCenter: l.costCenter || null,
                        taxCode: l.taxCode || null,
                        lineText: l.lineText || null
                    }))
            };
        },

        async onSimulate() {
            this._recalculate();
            await this._send("/api/v1/finance/journal-entries/simulate", (result) => {
                const model = this.getView().getModel();
                model.setProperty("/simulation", { visible: true, lines: result.lines });
                model.setProperty("/totals", {
                    debit: result.totals[0].debit,
                    credit: result.totals[0].credit,
                    difference: result.totals[0].difference
                });
                MessageToast.show(this.text("simulationOk"));
            });
        },

        async onPost() {
            this._recalculate();
            await this._send("/api/v1/finance/journal-entries", (result) => {
                MessageToast.show(this.text("postedOk", [result.documentNumberFormatted]));

                this.appContext().addRecent(this.getContext(), {
                    key: result.documentNumberFormatted,
                    title: result.documentNumberFormatted,
                    subtitle: this.text("journalEntryTitle"),
                    route: "journalDisplay",
                    params: {
                        companyCode: result.companyCode,
                        fiscalYear: result.fiscalYear,
                        documentNumber: result.documentNumber
                    }
                });

                this.getRouter().navTo("journalDisplay", {
                    companyCode: result.companyCode,
                    fiscalYear: result.fiscalYear,
                    documentNumber: result.documentNumber
                });
            }, /* idempotent */ true);
        },

        async _send(path, onSuccess, idempotent) {
            const view = this.getView();
            const model = view.getModel();
            view.setBusy(true);
            Messaging.removeAllMessages();
            model.setProperty("/messageCount", 0);

            try {
                const result = await this.api().request(path, {
                    method: "POST",
                    body: this._payload(),
                    context: this.contextData(),
                    // A retry after a dropped connection must not post twice.
                    idempotencyKey: idempotent
                        ? "ui-" + Date.now() + "-" + Math.random().toString(36).slice(2, 10)
                        : undefined
                });
                onSuccess(result);
            } catch (error) {
                this.api().report(error);
                model.setProperty("/messageCount", Messaging.getMessageModel().getData().length);
            } finally {
                view.setBusy(false);
            }
        },

        onShowMessages() {
            this._openMessages();
        },

        _openMessages() {
            if (!this._messagePopover) {
                this._messagePopover = new MessagePopover({
                    items: {
                        path: "message>/",
                        template: new MessageItem({
                            type: "{message>type}",
                            title: "{message>message}",
                            subtitle: "{message>additionalText}",
                            description: "{message>description}"
                        })
                    }
                });
                this.getView().setModel(Messaging.getMessageModel(), "message");
                this.getView().addDependent(this._messagePopover);
            }

            const button = this.byId("messagesButton");
            if (button.getVisible()) {
                this._messagePopover.openBy(button);
            }
        }
    });
});
