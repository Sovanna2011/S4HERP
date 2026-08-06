sap.ui.define([
    "sap/ui/core/mvc/Controller",
    "sap/ui/core/UIComponent",
    "s4herp/ui/model/Api",
    "s4herp/ui/model/AppContext"
], (Controller, UIComponent, Api, AppContext) => {
    "use strict";

    return Controller.extend("s4herp.ui.controller.BaseController", {
        getRouter() {
            return UIComponent.getRouterFor(this);
        },

        getContext() {
            return this.getOwnerComponent().getModel("context");
        },

        contextData() {
            return this.getContext().getData();
        },

        text(key, args) {
            return this.getOwnerComponent().getModel("i18n").getResourceBundle()
                .getText(key, args);
        },

        api() {
            return Api;
        },

        appContext() {
            return AppContext;
        },

        onNavBack() {
            const history = sap.ui.core.routing.History.getInstance();
            if (history.getPreviousHash() !== undefined) {
                window.history.go(-1);
            } else {
                this.getRouter().navTo("launchpad", {}, true);
            }
        }
    });
});
