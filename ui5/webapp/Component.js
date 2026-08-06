sap.ui.define([
    "sap/ui/core/UIComponent",
    "sap/ui/Device",
    "sap/ui/model/json/JSONModel",
    "s4herp/ui/model/AppContext"
], (UIComponent, Device, JSONModel, AppContext) => {
    "use strict";

    return UIComponent.extend("s4herp.ui.Component", {
        metadata: { manifest: "json", interfaces: ["sap.ui.core.IAsyncContentCreation"] },

        init(...args) {
            UIComponent.prototype.init.apply(this, args);

            this.setModel(new JSONModel({
                isPhone: Device.system.phone,
                isTouch: Device.support.touch
            }), "device");

            // The context bar — company code, fiscal year, user, language — is
            // owned by the shell and published once. Apps read it and must not
            // keep their own copy, or switching company code leaves one tile
            // showing stale data.
            this.setModel(AppContext.createModel(), "context");

            this.getRouter().initialize();
        },

        getContentDensityClass() {
            return Device.support.touch ? "sapUiSizeCozy" : "sapUiSizeCompact";
        }
    });
});
