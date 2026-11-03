import 'dart:async';

import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../appointments/appointments_page.dart';
import '../audit/audit_page.dart';
import '../clients/clients_page.dart';
import '../employees/employees_page.dart';
import '../gallery/gallery_page.dart';
import '../products/shop_sections_page.dart';
import '../notifications/notifications_page.dart';
import '../orders/orders_page.dart';
import '../profile/profile_page.dart';
import '../products/products_page.dart';
import '../reports/overview_page.dart';
import '../reports/reports_page.dart';
import '../reviews/reviews_page.dart';
import '../salon/salon_page.dart';
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
    required this.clients,
    required this.audit,
    required this.gallery,
    required this.salon,
    required this.user,
    required this.auth,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;
  final ShopApi shop;
  final NotificationsApi notifications;
  final ReviewsApi reviews;
  final ReportsApi reports;
  final ClientsApi clients;
  final AuditApi audit;
  final GalleryApi gallery;
  final SalonApi salon;
  final AppUser user;
  final AuthController auth;
}

class _Section {
  const _Section(
    this.icon,
    this.label, {
    required this.group,
    this.adminOnly = false,
    this.body,
    this.badge,
  });
  final IconData icon;
  final String Function(AppLocalizations) label;

  /// The heading this entry sits under in the sidebar (entries of one group are kept together).
  final String Function(AppLocalizations) group;
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
  late final ClientsApi _clients = ClientsApi(widget.auth.api);
  late final AuditApi _audit = AuditApi(widget.auth.api);
  late final GalleryApi _gallery = GalleryApi(widget.auth.api);
  late final SalonApi _salon = SalonApi(widget.auth.api);

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
      group: (t) => t.groupWork,
      adminOnly: true,
      body: (d) => OverviewPage(api: d.reports),
    ),
    _Section(
      Icons.schedule_outlined,
      (t) => t.appointments,
      group: (t) => t.groupWork,
      body: (d) => AppointmentsPage(
        catalog: d.catalog,
        reservations: d.reservations,
        isAdmin: d.user.isAdmin,
      ),
    ),
    _Section(
      Icons.notifications_outlined,
      (t) => t.notifications,
      group: (t) => t.groupWork,
      adminOnly: true,
      badge: 'notifications',
      body: (d) =>
          NotificationsPage(api: d.notifications, reservations: d.reservations),
    ),
    _Section(
      Icons.inventory_2_outlined,
      (t) => t.products,
      group: (t) => t.groupShop,
      adminOnly: true,
      badge: 'products',
      body: (d) => ProductsPage(shop: d.shop),
    ),
    _Section(
      Icons.receipt_long_outlined,
      (t) => t.orders,
      group: (t) => t.groupShop,
      adminOnly: true,
      badge: 'orders',
      body: (d) => OrdersPage(shop: d.shop),
    ),
    _Section(
      Icons.view_carousel_outlined,
      (t) => t.shopSectionsTitle,
      group: (t) => t.groupShop,
      adminOnly: true,
      body: (d) => ShopSectionsPage(shop: d.shop),
    ),
    _Section(
      Icons.list_alt_outlined,
      (t) => t.services,
      group: (t) => t.groupSalon,
      adminOnly: true,
      body: (d) => ServicesPage(catalog: d.catalog),
    ),
    _Section(
      Icons.content_cut,
      (t) => t.employees,
      group: (t) => t.groupSalon,
      adminOnly: true,
      body: (d) => EmployeesPage(catalog: d.catalog),
    ),
    _Section(
      Icons.photo_library_outlined,
      (t) => t.galleryTitle,
      group: (t) => t.groupSalon,
      adminOnly: true,
      body: (d) => GalleryPage(gallery: d.gallery, catalog: d.catalog),
    ),
    _Section(
      Icons.storefront_outlined,
      (t) => t.salonProfile,
      group: (t) => t.groupSalon,
      adminOnly: true,
      body: (d) => SalonPage(salon: d.salon),
    ),
    _Section(
      Icons.groups_outlined,
      (t) => t.clients,
      group: (t) => t.groupClients,
      adminOnly: true,
      body: (d) => ClientsPage(api: d.clients),
    ),
    _Section(
      Icons.star_outline,
      (t) => t.reviews,
      group: (t) => t.groupClients,
      adminOnly: true,
      body: (d) => ReviewsPage(api: d.reviews),
    ),
    _Section(
      Icons.payments_outlined,
      (t) => t.earnings,
      group: (t) => t.groupAnalytics,
      adminOnly: true,
      body: (d) => EarningsPage(api: d.reports),
    ),
    _Section(
      Icons.insert_chart_outlined,
      (t) => t.reports,
      group: (t) => t.groupAnalytics,
      adminOnly: true,
      body: (d) => ReportsPage(api: d.reports),
    ),
    _Section(
      Icons.history,
      (t) => t.auditLog,
      group: (t) => t.groupAnalytics,
      adminOnly: true,
      body: (d) => AuditPage(api: d.audit),
    ),
    _Section(
      Icons.person_outline,
      (t) => t.myProfile,
      group: (t) => t.groupAccount,
      body: (d) => ProfilePage(auth: d.auth),
    ),
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
                  group: s.group(t),
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
                          clients: _clients,
                          audit: _audit,
                          gallery: _gallery,
                          salon: _salon,
                          user: user,
                          auth: widget.auth,
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
