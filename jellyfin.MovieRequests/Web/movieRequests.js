(() => {
    "use strict";

    const apiRoot = "/MovieRequests";
    const searchInput = document.getElementById("searchInput");
    const searchResults = document.getElementById("searchResults");
    const searchMessage = document.getElementById("searchMessage");
    const requestList = document.getElementById("requestList");
    const listMessage = document.getElementById("listMessage");
    const requestCount = document.getElementById("requestCount");
    const refreshButton = document.getElementById("refreshButton");
    let searchTimer;
    let searchController;
    let isAdministrator = false;

    function serverAddress() {
        const address = window.ApiClient?.serverAddress?.() || window.location.origin;
        return address.replace(/\/+$/, "");
    }

    function accessToken() {
        return window.ApiClient?.accessToken?.() || "";
    }

    async function api(path, options = {}) {
        const headers = new Headers(options.headers || {});
        const token = accessToken();
        if (token) {
            headers.set("X-Emby-Token", token);
        }
        if (options.body) {
            headers.set("Content-Type", "application/json");
        }

        const response = await fetch(`${serverAddress()}${apiRoot}${path}`, {
            ...options,
            headers
        });
        const contentType = response.headers.get("content-type") || "";
        const body = contentType.includes("json") ? await response.json() : null;
        if (!response.ok) {
            const error = new Error(body?.detail || body?.title || `Request failed (${response.status}).`);
            error.status = response.status;
            throw error;
        }
        return body;
    }

    function showMessage(element, message, isError = false) {
        element.textContent = message;
        element.classList.toggle("error", isError);
    }

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function moviePoster(url, title, className) {
        if (!url) {
            return element("div", `poster-placeholder ${className || ""}`);
        }
        const image = element("img", className || "");
        image.src = url;
        image.alt = `Poster for ${title}`;
        image.loading = "lazy";
        image.onerror = () => image.replaceWith(element("div", "poster-placeholder"));
        return image;
    }

    function renderSearchResults(movies) {
        searchResults.replaceChildren();
        if (!movies.length) return;

        for (const movie of movies) {
            const card = element("article", "movie-result");
            card.append(moviePoster(movie.posterUrl, movie.title));
            const info = element("div", "movie-info");
            info.append(element("h3", "", movie.title));
            info.append(element("p", "year", movie.year ?? "Release year unknown"));
            const select = element("button", "button button-primary", "Request movie");
            select.type = "button";
            select.addEventListener("click", () => submitRequest(movie, select));
            info.append(select);
            card.append(info);
            searchResults.append(card);
        }
    }

    async function searchMovies(query) {
        searchController?.abort();
        searchController = new AbortController();
        if (query.length < 2) {
            searchResults.replaceChildren();
            showMessage(searchMessage, query ? "Enter at least 2 characters to search." : "");
            return;
        }

        showMessage(searchMessage, "Searching TMDb...");
        try {
            const movies = await api(`/search?query=${encodeURIComponent(query)}`, {
                signal: searchController.signal
            });
            renderSearchResults(movies || []);
            showMessage(searchMessage, movies?.length ? "" : "No matching movies found.");
        } catch (error) {
            if (error.name !== "AbortError") {
                renderSearchResults([]);
                showMessage(searchMessage, error.message, true);
            }
        }
    }

    async function submitRequest(movie, button) {
        button.disabled = true;
        showMessage(searchMessage, `Checking Jellyfin and requesting ${movie.title}...`);
        try {
            await api("/request", {
                method: "POST",
                body: JSON.stringify({ tmDbId: movie.tmDbId })
            });
            showMessage(searchMessage, `${movie.title} was added to the request list.`);
            searchInput.value = "";
            renderSearchResults([]);
            await loadRequests();
        } catch (error) {
            showMessage(searchMessage, error.message, true);
            button.disabled = false;
        }
    }

    function renderRequests(requests) {
        requestList.replaceChildren();
        requestCount.textContent = `${requests.length} ${requests.length === 1 ? "movie" : "movies"}`;
        if (!requests.length) {
            requestList.append(element("div", "empty-state", "No movies have been requested yet. Be the first to suggest one."));
            return;
        }

        for (const request of requests) {
            const card = element("article", "request-card");
            card.append(moviePoster(request.posterUrl, request.title));

            const info = element("div", "request-info");
            const titleRow = element("div", "request-title-row");
            titleRow.append(element("h3", "", request.title));
            if (request.isCompleted) {
                titleRow.append(element("span", "badge", "Completed"));
            }
            info.append(titleRow);
            const meta = request.year ? `${request.year} · ` : "";
            info.append(element("p", "request-meta", `${meta}Requested by ${request.username || request.requestedBy}`));
            card.append(info);

            const actions = element("div", "request-actions");
            actions.append(element("span", "vote-count", `${request.votes} ${request.votes === 1 ? "vote" : "votes"}`));
            const vote = element("button", "button button-primary", request.votedByCurrentUser ? "Voted" : "Vote");
            vote.type = "button";
            vote.disabled = request.votedByCurrentUser || request.isCompleted;
            vote.setAttribute("aria-label", `Vote for ${request.title}`);
            vote.addEventListener("click", async () => {
                vote.disabled = true;
                try {
                    await api(`/vote/${encodeURIComponent(request.id)}`, { method: "POST" });
                    await loadRequests();
                } catch (error) {
                    showMessage(listMessage, error.message, true);
                    vote.disabled = false;
                }
            });
            actions.append(vote);

            if (isAdministrator) {
                if (!request.isCompleted) {
                    const complete = element("button", "button button-secondary", "Mark completed");
                    complete.type = "button";
                    complete.addEventListener("click", async () => {
                        complete.disabled = true;
                        try {
                            await api(`/request/${encodeURIComponent(request.id)}/completed`, { method: "PUT" });
                            await loadRequests();
                        } catch (error) {
                            showMessage(listMessage, error.message, true);
                            complete.disabled = false;
                        }
                    });
                    actions.append(complete);
                }

                const remove = element("button", "button button-danger", "Delete");
                remove.type = "button";
                remove.addEventListener("click", async () => {
                    if (!window.confirm(`Delete the request for "${request.title}"?`)) return;
                    remove.disabled = true;
                    try {
                        await api(`/request/${encodeURIComponent(request.id)}`, { method: "DELETE" });
                        await loadRequests();
                    } catch (error) {
                        showMessage(listMessage, error.message, true);
                        remove.disabled = false;
                    }
                });
                actions.append(remove);
            }

            card.append(actions);
            requestList.append(card);
        }
    }

    async function loadRequests() {
        showMessage(listMessage, "Loading requests...");
        try {
            const requests = await api("/list");
            renderRequests(requests || []);
            showMessage(listMessage, "");
        } catch (error) {
            showMessage(listMessage, error.message, true);
        }
    }

    async function loadCapabilities() {
        try {
            const capabilities = await api("/capabilities");
            isAdministrator = capabilities?.isAdministrator === true;
        } catch (error) {
            showMessage(listMessage, error.message, true);
        }
    }

    searchInput.addEventListener("input", () => {
        window.clearTimeout(searchTimer);
        const query = searchInput.value.trim();
        searchTimer = window.setTimeout(() => searchMovies(query), 300);
    });
    refreshButton.addEventListener("click", loadRequests);

    loadCapabilities().then(loadRequests);
})();
