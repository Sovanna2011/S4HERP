sap.ui.define([
    "s4herp/ui/controller/BaseController"
], (BaseController) => {
    "use strict";

    return BaseController.extend("s4herp.ui.controller.App", {
        onInit() {
            this.getView().addStyleClass(this.getOwnerComponent().getContentDensityClass());
        }
    });
});
