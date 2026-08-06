sap.ui.define(["sap/ui/model/json/JSONModel"], (JSONModel) => {
    "use strict";

    const STORAGE_KEY = "s4herp.context";

    /**
     * Shell-owned session context: which company code, fiscal year, user and
     * language every application works in. Persisted so a reload does not throw
     * the user back to defaults.
     */
    return {
        createModel() {
            const stored = this._read();
            const model = new JSONModel(Object.assign({
                companyCode: "1000",
                fiscalYear: new Date().getUTCFullYear(),
                // Development identity. Replaced by the authenticated subject
                // once OIDC is in place; the server is what enforces authority
                // either way.
                userName: "seed.accountant",
                language: "en",
                favourites: [],
                recents: []
            }, stored));

            model.attachPropertyChange(() => this._write(model.getData()));
            return model;
        },

        addRecent(model, entry) {
            const recents = (model.getProperty("/recents") || [])
                .filter((r) => r.key !== entry.key);
            recents.unshift(entry);
            model.setProperty("/recents", recents.slice(0, 6));
            this._write(model.getData());
        },

        toggleFavourite(model, key) {
            const favourites = model.getProperty("/favourites") || [];
            const index = favourites.indexOf(key);
            if (index >= 0) {
                favourites.splice(index, 1);
            } else {
                favourites.push(key);
            }
            model.setProperty("/favourites", favourites.slice());
            this._write(model.getData());
        },

        _read() {
            try {
                return JSON.parse(window.localStorage.getItem(STORAGE_KEY)) || {};
            } catch (e) {
                return {};
            }
        },

        _write(data) {
            try {
                window.localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
            } catch (e) {
                // Private browsing or a full quota: the context simply does not
                // persist. Not worth interrupting the user for.
            }
        }
    };
});
