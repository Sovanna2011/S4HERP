sap.ui.define([
    "sap/ui/core/mvc/Controller",
    "sap/ui/core/UIComponent",
    "s4herp/ui/model/Api",
    "s4herp/ui/model/AppContext",
    "sap/m/Dialog",
    "sap/m/Button",
    "sap/m/TextArea"
], (Controller, UIComponent, Api, AppContext, Dialog, Button, TextArea) => {
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

        /**
         * Asks for the comment that goes on the approval step. Built as a dialog
         * rather than a MessageBox because MessageBox cannot take free text, and a
         * rejection reason typed by the approver is the whole value of the record.
         */
        _promptComment(titleKey, labelKey, required, onConfirm) {
            const input = new TextArea({
                width: "100%",
                rows: 3,
                placeholder: this.text(labelKey),
                valueLiveUpdate: true
            });

            const confirm = new Button({
                text: this.text("confirm"),
                type: "Emphasized",
                // Required means required here too. The server refuses an empty
                // rejection reason, and finding that out after a round trip helps
                // nobody.
                enabled: !required,
                press: () => {
                    dialog.close();
                    onConfirm(input.getValue());
                }
            });

            if (required) {
                input.attachLiveChange(() =>
                    confirm.setEnabled(input.getValue().trim().length > 0));
            }

            const dialog = new Dialog({
                id: this.createId("commentDialog"),
                title: this.text(titleKey),
                contentWidth: "24rem",
                content: [input],
                beginButton: confirm,
                endButton: new Button({
                    text: this.text("cancel"),
                    press: () => dialog.close()
                }),
                afterClose: () => dialog.destroy()
            });

            this.getView().addDependent(dialog);
            dialog.open();
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
