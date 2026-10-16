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
  const _Section(this.icon, this.label, {this.adminOnly = false, this.body});
  final IconData icon;
  final String Function(AppLocalizations) label;
  final bool adminOnly;

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
      body: (d) =>
          NotificationsPage(api: d.notifications, reservations: d.reservations),
    ),
    _Section(
      Icons.inventory_2_outlined,
      (t) => t.products,
      adminOnly: true,
      body: (d) => ProductsPage(shop: d.shop),
    ),
    _Section(
      Icons.receipt_long_outlined,
      (t) => t.orders,
      adminOnly: true,
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
          NavigationRail(
            selectedIndex: index,
            onDestinationSelected: (i) => setState(() => _index = i),
            labelType: NavigationRailLabelType.all,
            leading: Padding(
              padding: const EdgeInsets.symmetric(vertical: 16),
              child: Text(
                t.appName,
                style: const TextStyle(
                  fontWeight: FontWeight.w800,
                  letterSpacing: 2,
                ),
              ),
            ),
            trailing: Expanded(
              child: Align(
                alignment: Alignment.bottomCenter,
                child: Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: IconButton(
                    key: const Key('logout'),
                    tooltip: t.logout,
                    icon: const Icon(Icons.logout),
                    onPressed: widget.auth.logout,
                  ),
                ),
              ),
            ),
            destinations: [
              for (final s in visible)
                NavigationRailDestination(
                  icon: Icon(s.icon),
                  label: Text(s.label(t)),
                ),
            ],
          ),
          const VerticalDivider(width: 1),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(32, 24, 32, 8),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          current.label(t),
                          style: Theme.of(context).textTheme.headlineSmall
                              ?.copyWith(fontWeight: FontWeight.w700),
                        ),
                      ),
                      Text(t.signedInAs(user.fullName)),
                    ],
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
