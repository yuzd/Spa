(function (e) {
    function n(b) {
        e(b).on("click" + p, this.toggle)
    }

    function q(b, a) {
        if (g) {
            a || (a = [g]);
            var c;
            g[0] !== a[0][0] ? c = g : (c = a[a.length - 1], c.parent().hasClass(k) && (c = c.parent()));
            c.find("." + f).removeClass(f);
            c.hasClass(f) && c.removeClass(f);
            c === g && (g = null, e(l).remove())
        }
    }

    function r(b) {
        var a = b.attr("data-target");
        a || (a = (a = b.attr("href")) && /#[A-Za-z]/.test(a) && a.replace(/.*(?=#[^\s]*$)/, ""));
        return (a = a && e(a)) && a.length ? a : b.parent()
    }

    var l = ".dropdown-backdrop", k = "dropdown-menu", p = ".bs.dropdown", f = "open", s = "ontouchstart" in
        document.documentElement, g, h = n.prototype;
    h.toggle = function (b) {
        var a = e(this);
        if (!a.is(".disabled, :disabled")) {
            var a = r(a), c = a.hasClass(f), d;
            if (a.hasClass("dropdown-submenu")) {
                for (var m = [a]; !d || d.hasClass("dropdown-submenu");)d = (d || a).parent(), d.hasClass(k) && (d = d.parent()), d.children('[data-toggle="dropdown"]') && m.unshift(d);
                d = m
            } else d = null;
            q(b, d);
            if (!c) {
                d || (d = [a]);
                if (s && !a.closest(".navbar-nav").length && !d[0].find(l).length)e('<div class="' + l.substr(1) + '"/>').appendTo(d[0]).on("click", q);
                b = 0;
                for (a =
                         d.length; b < a; b++)d[b].hasClass(f) || (d[b].addClass(f), c = d[b].children("." + k), m = d[b], c.hasClass("pull-center") && c.css("margin-right", c.outerWidth() / -2), c.hasClass("pull-middle") && c.css("margin-top", c.outerHeight() / -2 - m.outerHeight() / 2));
                g = d[0]
            }
            return !1
        }
    };
    h.keydown = function (b) {
        if (/(38|40|27)/.test(b.keyCode)) {
            var a = e(this);
            b.preventDefault();
            b.stopPropagation();
            if (!a.is(".disabled, :disabled")) {
                var c = r(a), d = c.hasClass("open");
                if (!d || d && 27 == b.keyCode)return 27 == b.which && c.find('[data-toggle="dropdown"]').trigger("focus"),
                    a.trigger("click");
                a = c.find('li:not(.divider):visible > input:not(disabled) ~ label, [role="menu"] li:not(.divider):visible a, [role="listbox"] li:not(.divider):visible a');
                a.length && (c = a.index(a.filter(":focus")), 38 == b.keyCode && 0 < c && c--, 40 == b.keyCode && c < a.length - 1 && c++, ~c || (c = 0), a.eq(c).trigger("focus"))
            }
        }
    };
    h.change = function (b) {
        var a, c = "";
        a = e(this).closest("." + k);
        (b = a.parent().find("[data-label-placement]")) && b.length || (b = a.parent().find('[data-toggle="dropdown"]'));
        b && b.length && !1 !== b.data("placeholder") &&
        (void 0 == b.data("placeholder") && b.data("placeholder", e.trim(b.text())), c = e.data(b[0], "placeholder"), a = a.find("li > input:checked"), a.length && (c = [], a.each(function () {
            var a = e(this).parent().find("label").eq(0), b = a.find(".data-label");
            b.length && (a = e("<p></p>"), a.append(b.clone()));
            (a = a.html()) && c.push(e.trim(a))
        }), c = 4 > c.length ? c.join(", ") : c.length + " selected"), a = b.find(".caret"), b.html(c || "&nbsp;"), a.length && b.append(" ") && a.appendTo(b))
    };
    var t = e.fn.dropdown;
    e.fn.dropdown = function (b) {
        return this.each(function () {
            var a =
                e(this), c = a.data("bs.dropdown");
            c || a.data("bs.dropdown", c = new n(this));
            "string" == typeof b && c[b].call(a)
        })
    };
    e.fn.dropdown.Constructor = n;
    e.fn.dropdown.clearMenus = function (b) {
        e(l).remove();
        e("." + f + ' [data-toggle="dropdown"]').each(function () {
            var a = r(e(this)), c = {relatedTarget: this};
            a.hasClass("open") && (a.trigger(b = e.Event("hide" + p, c)), b.isDefaultPrevented() || a.removeClass("open").trigger("hidden" + p, c))
        });
        return this
    };
    e.fn.dropdown.noConflict = function () {
        e.fn.dropdown = t;
        return this
    };
    e(document).off(".bs.dropdown.data-api").on("click.bs.dropdown.data-api",
        q).on("click.bs.dropdown.data-api", '[data-toggle="dropdown"]', h.toggle).on("click.bs.dropdown.data-api", '.dropdown-menu > li > input[type="checkbox"] ~ label, .dropdown-menu > li > input[type="checkbox"], .dropdown-menu.noclose > li', function (b) {
        b.stopPropagation()
    }).on("change.bs.dropdown.data-api", '.dropdown-menu > li > input[type="checkbox"], .dropdown-menu > li > input[type="radio"]', h.change).on("keydown.bs.dropdown.data-api", '[data-toggle="dropdown"], [role="menu"], [role="listbox"]', h.keydown)
})(jQuery);