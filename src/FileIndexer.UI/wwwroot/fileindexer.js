// Browser helpers for the shared FileIndexer components (see Services/FileIndexerJs.cs).
window.fileIndexer = {
    // Drag handles on table headers to resize columns.
    initColumnResize: function (tableSelector) {
        var table = document.querySelector(tableSelector);
        if (!table) return;
        table.querySelectorAll('th').forEach(function (th) {
            if (th.querySelector('.col-resize-handle')) return;
            var handle = document.createElement('div');
            handle.className = 'col-resize-handle';
            th.appendChild(handle);
            handle.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });
            handle.addEventListener('mousedown', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var startX = e.pageX;
                var startW = th.offsetWidth;
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
                var onMove = function (e2) {
                    var w = startW + (e2.pageX - startX);
                    if (w > 30) th.style.width = w + 'px';
                };
                var onUp = function () {
                    document.body.style.cursor = '';
                    document.body.style.userSelect = '';
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup', onUp);
                };
                document.addEventListener('mousemove', onMove);
                document.addEventListener('mouseup', onUp);
            });
        });
    },

    // Keeps the context menu inside the viewport once it has been rendered.
    adjustContextMenu: function () {
        var menu = document.querySelector('.context-menu');
        if (!menu) return;
        var rect = menu.getBoundingClientRect();
        var margin = 8;
        if (rect.bottom > window.innerHeight - margin) {
            menu.style.top = Math.max(margin, window.innerHeight - rect.height - margin) + 'px';
        }
        if (rect.right > window.innerWidth - margin) {
            menu.style.left = Math.max(margin, window.innerWidth - rect.width - margin) + 'px';
        }
    },

    // Theme: class on <html>, persisted in localStorage. Host pages load this script in <head>
    // and call applyStoredTheme() before first paint (no flash of the wrong theme).
    isLightTheme: function () {
        try { return localStorage.getItem('theme') === 'light'; } catch (e) { return false; }
    },
    setLightTheme: function (light) {
        document.documentElement.classList.toggle('light-theme', light);
        try { localStorage.setItem('theme', light ? 'light' : 'dark'); } catch (e) { }
    },
    applyStoredTheme: function () {
        document.documentElement.classList.toggle('light-theme', window.fileIndexer.isLightTheme());
    },

    // Focuses the inline rename box and selects the name without its extension.
    selectRenameInput: function (selectionEnd) {
        var input = document.querySelector('.rename-input');
        if (!input) return;
        input.focus();
        input.setSelectionRange(0, selectionEnd);
    },

    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (e) {
            return false;
        }
    },

    downloadText: function (fileName, contentType, content) {
        var blob = new Blob([content], { type: contentType });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    // Resolves with the text of the chosen file, or null when the dialog is dismissed.
    pickTextFile: function (accept) {
        return new Promise(function (resolve) {
            var input = document.createElement('input');
            input.type = 'file';
            input.accept = accept;
            input.style.display = 'none';
            var done = function (value) {
                input.remove();
                resolve(value);
            };
            input.addEventListener('cancel', function () { done(null); });
            input.addEventListener('change', function () {
                var file = input.files && input.files[0];
                if (!file) { done(null); return; }
                file.text().then(done, function () { done(null); });
            });
            document.body.appendChild(input);
            input.click();
        });
    }
};
