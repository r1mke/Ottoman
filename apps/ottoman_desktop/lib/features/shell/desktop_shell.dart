import 'dart:async';

import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../appointments/appointments_page.dart';
import '../employees/employees_page.dart';
import '../notifications/notifications_page.dart';
import '../orders/orders_page.dart';
import '../products/products_page.dart';
import '../reports/overview_page.dart';
import '../reports/reports_page.dart';
import '../reviews/reviews_page.dart';
import '../services/services_page.dart';
import 'sidebar.dart';

/// Everything a section may need, built once from the session.
class ShellDeps {
  const ShellDeps({
    required this.catalog,
    required this.reservations,
    required this.shop,
    required this.notifications,
    required this.reviews,
    required this.reports,
    required this.user,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;
  final ShopApi shop;
  final NotificationsApi notifications;
  final ReviewsApi reviews;
  final ReportsApi reports;
  final AppUser user;
}

class _Section {
  const _Section(
    this.icon,
    this.label, {
    this.adminOnly = false,
    this.body,
    this.badge,
  });
  final IconData icon;
  final String Function(AppLocalizations) label;
  final bool adminOnly;

  /// Which live count (see [_DesktopShellState._refreshBadges]) this entry shows.
  final String? badge;

  /// Null renders the "coming soon" placeholder.
  final Widget Function(ShellDeps deps)? body;
}

/// Left navigation + content area. Sections are filtered by role: staff see only
/// their own schedule and profile, admins see everything.
class DesktopShell extends StatefulWidget {
  const DesktopShell({
    super.key,
    required this.auth,
    this.catalogOverride,
    this.shopOverride,
  });

  final AuthController auth;

  /// Test seam; built from the session when null.
  final CatalogApi? catalogOverride;
  final ShopApi? shopOverride;

  @override
  State<DesktopShell> createState() => _DesktopShellState();
}

class _DesktopShellState extends State<DesktopShell> {
  int _index = 0;
  late final CatalogApi _catalog =
      widget.catalogOverride ?? CatalogApi(widget.auth.api);
  late final ReservationsApi _reservations = ReservationsApi(widget.auth.api);
  late final ShopApi _shop = widget.shopOverride ?? ShopApi(widget.auth.api);
  late final NotificationsApi _notifications = NotificationsApi(
    widget.auth.api,
  );
  late final ReviewsApi _reviews = ReviewsApi(widget.auth.api);
  late final ReportsApi _reports = ReportsApi(widget.auth.api);

  /// Live counts for the sidebar: unread messages, products at or under their stock threshold,
  /// and orders waiting to be handed over. Only an admin sees these sections, so only an admin asks.
  Map<String, int> _badges = const {};
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    if (widget.auth.user?.isAdmin ?? false) {
      _refreshBadges();
      _poll = Timer.periodic(
        const Duration(seconds: 60),
        (_) => _refreshBadges(),
      );
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _refreshBadges() async {
    if (!(widget.auth.user?.isAdmin ?? false)) return;
    Future<int> safe(Future<int> Function() load) async {
      try {
        return await load();
      } on Object {
        // A badge is a convenience; a failed count just shows nothing.
        return 0;
      }
    }

    final counts = await Future.wait([
      safe(_notifications.unreadCount),
      safe(
        () async =>
            (await _shop.products(lowStock: true, pageSize: 1)).totalCount,
      ),
      safe(
        () async => (await _shop.orders(
          status: OrderStatus.placed,
          pageSize: 1,
        )).totalCount,
      ),
    ]);
    if (!mounted) return;
    setState(
      () => _badges = {
        'notifications': counts[0],
        'products': counts[1],
        'orders': counts[2],
      },
    );
  }

  static final _sections = <_Section>[
    _Section(
      Icons.dashboard_outlined,
      (t) => t.overview,
      adminOnly: true,
      body: (d) => OverviewPage(api: d.reports),
    ),
    _Section(
      Icons.schedule_outlined,
      (t) => t.appointments,
      body: (d) => AppointmentsPage(
        catalog: d.catalog,
        reservations: d.reservations,
        isAdmin: d.user.isAdmin,
      ),
    ),
    _Section(
      Icons.payments_outlined,
      (t) => t.earnings,
      adminOnly: true,
      body: (d) => EarningsPage(api: d.reports),
    ),
    _Section(
      Icons.list_alt_outlined,
      (t) => t.services,
      adminOnly: true,
      body: (d) => ServicesPage(catalog: d.catalog),
    ),
    _Section(
      Icons.notifications_outlined,
      (t) => t.notifications,
      adminOnly: true,
      badge: 'notifications',
      body: (d) =>
          NotificationsPage(api: d.notifications, reservations: d.reservations),
    ),
    _Section(
      Icons.inventory_2_outlined,
      (t) => t.products,
      adminOnly: true,
      badge: 'products',
      body: (d) => ProductsPage(shop: d.shop),
    ),
    _Section(
      Icons.receipt_long_outlined,
      (t) => t.orders,
      adminOnly: true,
      badge: 'orders',
      body: (d) => OrdersPage(shop: d.shop),
    ),
    _Section(
      Icons.star_outline,
      (t) => t.reviews,
      adminOnly: true,
      body: (d) => ReviewsPage(api: d.reviews),
    ),
    _Section(Icons.groups_outlined, (t) => t.clients, adminOnly: true),
    _Section(
      Icons.content_cut,
      (t) => t.employees,
      adminOnly: true,
      body: (d) => EmployeesPage(catalog: d.catalog),
    ),
    _Section(
      Icons.insert_chart_outlined,
      (t) => t.reports,
      adminOnly: true,
      body: (d) => ReportsPage(api: d.reports),
    ),
    _Section(Icons.person_outline, (t) => t.myProfile),
  ];

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final user = widget.auth.user!;
    final visible = _sections
        .where((s) => !s.adminOnly || user.isAdmin)
        .toList();
    final index = _index.clamp(0, visible.length - 1);
    final current = visible[index];

    return Scaffold(
      body: Row(
        children: [
          Sidebar(
            selected: index,
            onSelect: (i) {
              setState(() => _index = i);
              _refreshBadges();
            },
            userName: user.fullName,
            onLogout: widget.auth.logout,
            items: [
              for (final s in visible)
                SidebarItem(
                  icon: s.icon,
                  label: s.label(t),
                  badge: _badges[s.badge] ?? 0,
                ),
            ],
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(36, 28, 36, 8),
                  child: Text(
                    current.label(t),
                    key: const Key('page_title'),
                    style: Theme.of(context).textTheme.headlineMedium
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                ),
                Expanded(
                  child:
                      current.body?.call(
                        ShellDeps(
                          catalog: _catalog,
                          reservations: _reservations,
                          shop: _shop,
                          notifications: _notifications,
                          reviews: _reviews,
                          reports: _reports,
                          user: user,
                        ),
                      ) ??
                      EmptyState(icon: current.icon, message: t.comingSoon),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
