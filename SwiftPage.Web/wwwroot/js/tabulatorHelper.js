window.tabulatorHelper = {
    // Tabulator 인스턴스 캐시
    instances: {},

    // 내부 공용 헬퍼: 인스턴스 조회
    getTable: function (elementId) {
        return this.instances[elementId] || null;
    },

    init: function (elementId, columns, data, options) {
        // Tabulator가 로드되었는지 확인
        if (typeof Tabulator === 'undefined') {
            console.error('Tabulator library is not loaded');
            setTimeout(() => this.init(elementId, columns, data, options), 100);
            return;
        }

        const defaultOptions = {
            layout: "fitColumns",
            responsiveLayout: "hide",
            pagination: false,
            paginationSize: 10,
            paginationSizeSelector: [5, 10, 20],
            movableColumns: true,
            resizableColumns: true,
            ...options
        };

        const tableElement = document.getElementById(elementId);
        if (!tableElement) {
            console.error(`Element with id '${elementId}' not found`);
            return;
        }

        // 요소가 숨겨져 있는지 확인 (display: none 또는 visibility: hidden)
        const isHidden = tableElement.offsetParent === null;
        if (isHidden) {
            // 요소가 숨겨져 있으면 나중에 다시 시도
            console.warn(`Element '${elementId}' is hidden, will retry initialization`);
            setTimeout(() => this.init(elementId, columns, data, options), 300);
            return;
        }

        // 기존 인스턴스가 있으면 제거
        if (this.instances[elementId]) {
            this.instances[elementId].destroy();
        }

        // 컬럼 처리 및 이벤트 핸들링 (불필요한 옵션을 최소화해 가독성 향상)
        const processedColumns = (columns || []).map(col => {
            const processedCol = { ...(col || {}) };
            
            // HTML 포맷터 처리
            if (col.formatter === "html") {
                processedCol.formatter = (cell, formatterParams) => {
                    const value = cell.getValue();
                    // HTML 문자열을 반환
                    return value;
                };
                processedCol.cellClick = function(e, cell) {
                    // 버튼 클릭 이벤트 위임 처리
                    const target = e.target;
                    if (target.classList.contains('tabulator-action-btn')) {
                        e.stopPropagation();
                        const action = target.getAttribute('data-action');
                        const id = Number.parseInt(target.getAttribute('data-id') ?? "", 10);
                        
                        if (window.blazorTabulator) {
                            // 현재 시나리오에서 사용하는 액션만 유지
                            switch (action) {
                                case 'delete':
                                    window.blazorTabulator.deleteEmployee(id);
                                    break;
                                case 'openEdit':
                                    window.blazorTabulator.openEditDialog(id);
                                    break;
                            }
                        }
                    }
                };
            }

            // 부서 전용 커스텀 셀렉트 헤더 필터 (전체 + 부서 목록)
            if (col.headerFilter === "departmentSelect" && col.headerFilterParams && Array.isArray(col.headerFilterParams.values)) {
                processedCol.headerFilter = function (cell, onRendered, success, cancel) {
                    const select = document.createElement("select");
                    select.style.padding = "2px 4px";
                    select.style.width = "100%";

                    // 전체 옵션 (필터 해제)
                    const allOption = document.createElement("option");
                    allOption.value = "";
                    allOption.textContent = "전체";
                    select.appendChild(allOption);

                    // 실제 부서 옵션들
                    col.headerFilterParams.values.forEach(v => {
                        const opt = document.createElement("option");
                        opt.value = v ?? "";
                        opt.textContent = v ?? "";
                        select.appendChild(opt);
                    });

                    select.addEventListener("change", (e) => {
                        const value = e.target.value;
                        // 빈 값이면 필터를 해제 (전체)
                        if (!value) {
                            success("");
                        } else {
                            success(value);
                        }
                    });

                    onRendered(() => {
                        select.value = "";
                    });

                    return select;
                };

                processedCol.headerFilterFunc = "=";
            }
            // headerFilterParams 처리 (리스트/셀렉트 필터용 - 공통)
            else if ((col.headerFilter === "select" || col.headerFilter === "list") && col.headerFilterParams) {
                // Tabulator의 list 헤더 필터를 사용하도록 통일
                processedCol.headerFilter = "list";

                // headerFilterParams의 values 배열을 그대로 전달
                if (col.headerFilterParams.values && Array.isArray(col.headerFilterParams.values)) {
                    processedCol.headerFilterParams = {
                        values: col.headerFilterParams.values,
                        clearable: col.headerFilterParams.clearable !== undefined ? col.headerFilterParams.clearable : true
                    };
                }
            }
            
            return processedCol;
        });

        // Tabulator 인스턴스 생성
        const table = new Tabulator(tableElement, {
            columns: processedColumns,
            data: data,
            ...defaultOptions
        });

        // cellEdited 이벤트 핸들러 (Tab 키로 저장)
        if (options && options.onCellEdited) {
            table.on("cellEdited", function(cell) {
                const rowData = cell.getRow().getData();
                if (window.blazorTabulatorRef) {
                    window.blazorTabulatorRef.invokeMethodAsync('HandleCellEdited', elementId, rowData);
                }
            });
        }

        // 삭제 버튼 클릭 이벤트 (buttonCross 포맷터)
        table.on("cellClick", function(e, cell) {
            if (cell.getField() === "actions") {
                e.stopPropagation();
                const rowData = cell.getRow().getData();
                if (window.blazorTabulatorRef && rowData.id) {
                    window.blazorTabulatorRef.invokeMethodAsync('HandleDelete', rowData.id);
                }
            }
        });

        this.instances[elementId] = table;
        return true;
    },

    setData: function (elementId, data) {
        const table = this.getTable(elementId);
        if (!table) return false;

        table.setData(data || []);
        return true;
    },

    destroy: function (elementId) {
        const table = this.getTable(elementId);
        if (!table) return false;

        table.destroy();
        delete this.instances[elementId];
        return true;
    },

    clearFilter: function (elementId) {
        const table = this.getTable(elementId);
        if (!table) return false;

        // 행 필터 제거
        if (typeof table.clearFilter === "function") {
            try {
                table.clearFilter();
            } catch (e) {
                console.warn("tabulatorHelper.clearFilter: clearFilter 호출 중 오류", e);
            }
        }

        // 헤더 필터(입력창/셀렉트 박스) 제거
        if (typeof table.clearHeaderFilter === "function") {
            try {
                table.clearHeaderFilter();
            } catch (e) {
                console.warn("tabulatorHelper.clearFilter: clearHeaderFilter 호출 중 오류", e);
            }
        }

        return true;
    }
};

// Blazor 콜백을 위한 전역 객체
window.blazorTabulatorRef = null;

window.setBlazorTabulatorRef = function(dotNetRef) {
    window.blazorTabulatorRef = dotNetRef;
    window.blazorTabulator = {
        deleteEmployee: function(id) {
            if (window.blazorTabulatorRef) {
                window.blazorTabulatorRef.invokeMethodAsync('HandleDelete', id);
            }
        },
        openEditDialog: function(id) {
            if (window.blazorTabulatorRef) {
                window.blazorTabulatorRef.invokeMethodAsync('HandleOpenEditDialog', id);
            }
        }
    };
};