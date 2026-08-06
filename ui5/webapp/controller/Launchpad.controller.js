sap.ui.define([
    "s4herp/ui/controller/BaseController",
    "sap/ui/model/json/JSONModel",
    "sap/ui/core/Core",
    "sap/m/MessageToast"
], (BaseController, JSONModel, Core, MessageToast) => {
    "use strict";

    /**
     * Launchpad-style home. Tiles are declared here for now; Phase 3's T-code
     * registry already serves them from cfg.TransactionCode, and wiring the tile
     * list to that endpoint is what makes the launchpad role-based rather than
     * hard-coded.
     */
    const TILES = [
        {
            key: "FB50", name: "journalEntryTile", transactionCode: "FB50",
            module: "Finance", icon: "sap-icon://post", route: "journalCreate"
        },
        {
            key: "TRIAL", name: "trialBalanceTile", transactionCode: "F.01",
            module: "Finance", icon: "sap-icon://table-view", route: "trialBalance"
        }
    ];

    return BaseController.extend("s4herp.ui.controller.Launchpad", {
        onInit() {
            const bundle = this.getOwnerComponent().getModel("i18n").getResourceBundle();
            this.getView().setModel(new JSONModel({
                tiles: TILES.map((t) => Object.assign({}, t, { name: bundle.getText(t.name) }))
            }));
        },

        onTilePress(event) {
            const tile = event.getSource().getBindingContext().getObject();
            this.getRouter().navTo(tile.route);
        },

        onContextChanged() {
            // Persist immediately: a user who picks company code 2000 and then
            // opens a document expects 2000, not the default.
            this.appContext()._write(this.contextData());
        },

        onToggleLanguage() {
            const context = this.getContext();
            const next = context.getProperty("/language") === "en" ? "km" : "en";
            context.setProperty("/language", next);
            this.appContext()._write(context.getData());

            // The resource bundle is chosen at bootstrap, so a language change
            // needs a reload. Honest and simple; a live switch would mean
            // re-creating every bound view.
            const url = new URL(window.location.href);
            url.searchParams.set("sap-language", next);
            window.location.replace(url.toString());
        },

        onGlobalSearch(event) {
            const query = (event.getParameter("query") || "").trim().toUpperCase();
            if (!query) {
                return;
            }

            // An exact transaction-code match wins: someone typing FB50 wants the
            // transaction, not a list of things containing "FB50".
            const tile = TILES.find((t) => t.transactionCode.toUpperCase() === query
                || t.key.toUpperCase() === query);
            if (tile) {
                this.getRouter().navTo(tile.route);
                return;
            }

            MessageToast.show(this.text("searchNoMatch", [query]));
        },

        onRecentPress(event) {
            const recent = event.getSource().getBindingContext("context").getObject();
            this.getRouter().navTo(recent.route, recent.params || {});
        }
    });
});
