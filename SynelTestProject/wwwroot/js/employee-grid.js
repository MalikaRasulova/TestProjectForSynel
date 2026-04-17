(function () {
    let grid;

    const statusPanel = document.getElementById("importStatus");
    const importForm = document.getElementById("importForm");
    const searchBox = document.getElementById("gridSearch");

    function setStatus(message, kind) {
        statusPanel.textContent = message;
        statusPanel.classList.remove("is-error", "is-success");
        if (kind) {
            statusPanel.classList.add(kind);
        }
    }

    function buildColumns(columns) {
        return [
            {
                title: "ID",
                field: "EmployeeId",
                hozAlign: "right",
                width: 90,
                headerSort: true
            },
            ...columns.map(function (column) {
                return {
                    title: column.sourceName,
                    field: column.databaseName,
                    editor: "input",
                    headerSort: true,
                    headerFilter: "input"
                };
            })
        ];
    }

    async function fetchGrid() {
        const response = await fetch("/employees", {
            headers: {
                "Accept": "application/json"
            }
        });

        if (!response.ok) {
            throw new Error("Failed to load employees.");
        }

        return await response.json();
    }

    async function loadGrid() {
        const payload = await fetchGrid();
        const columns = buildColumns(payload.columns || []);

        if (!grid) {
            grid = new Tabulator("#employeeGrid", {
                layout: "fitDataStretch",
                reactiveData: true,
                data: payload.rows || [],
                columns: columns,
                pagination: true,
                paginationSize: 10,
                movableColumns: true,
                placeholder: "No employees imported yet."
            });

            grid.on("cellEdited", async function (cell) {
                const row = cell.getRow().getData();
                const employeeId = row.EmployeeId || row.employeeId;
                const body = {
                    values: {
                        [cell.getField()]: cell.getValue()
                    }
                };

                try {
                    const response = await fetch("/employees/" + employeeId, {
                        method: "POST",
                        headers: {
                            "Content-Type": "application/json"
                        },
                        body: JSON.stringify(body)
                    });

                    if (!response.ok) {
                        throw new Error();
                    }

                    setStatus("Employee row updated.", "is-success");
                } catch (error) {
                    cell.restoreOldValue();
                    setStatus("Could not save the edited value.", "is-error");
                }
            });

            return;
        }

        grid.setColumns(columns);
        grid.replaceData(payload.rows || []);
    }

    importForm.addEventListener("submit", async function (event) {
        event.preventDefault();

        const formData = new FormData(importForm);
        setStatus("Importing rows...");

        try {
            const response = await fetch("/employees/import", {
                method: "POST",
                body: formData
            });

            const payload = await response.json();
            if (!response.ok) {
                throw new Error(payload.message || "Import failed.");
            }

            setStatus(payload.rowsProcessed + " row(s) imported successfully.", "is-success");
            await loadGrid();
            applySearch();
        } catch (error) {
            setStatus(error.message || "Import failed.", "is-error");
        }
    });

    function applySearch() {
        const query = searchBox.value.trim().toLowerCase();
        if (!grid) {
            return;
        }

        if (!query) {
            grid.clearFilter(true);
            return;
        }

        grid.setFilter(function (data) {
            return Object.keys(data).some(function (key) {
                const value = data[key];
                return value && value.toString().toLowerCase().includes(query);
            });
        });
    }

    searchBox.addEventListener("input", applySearch);

    loadGrid().catch(function () {
        setStatus("Grid initialization failed. Check the SQL Server connection.", "is-error");
    });
})();
