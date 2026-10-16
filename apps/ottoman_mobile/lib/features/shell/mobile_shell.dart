import 'dart:async';

import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../booking/employees_tab.dart';
import '../notifications/notifications_page.dart';
import '../profile/profile_page.dart';
import '../reservations/employee_schedule_tab.dart';
import '../reservations/my_reservations_tab.dart';
import '../services/pricelist_tab.dart';
import '../shop/shop_tab.dart';

class _Tab {
  const _Tab(this.icon, this.label, this.body);
  final IconData icon;
  final String Function(AppLocalizations) label;
  final Widget Function() body;
}

/// Clients and employees get different navigation: clients book and shop,
/// employees only see their schedule, services and notifications.
class MobileShell extends StatefulWidget {
  const MobileShell({
    super.key,
    required this.auth,
    this.catalogOverride,
    this.shopOverride,
    this.cardPayment = const UnavailableCardPayment(),
  });

  final AuthController auth;

  /// Test seam; the shell builds its own [CatalogApi] from the session when null.
  final CatalogApi? catalogOverride;

  final ShopApi? shopOverride;

  /// Card payments need the Stripe SDK; until one is plugged in, card checkout is hidden.
  final CardPaymentLauncher cardPayment;

  @override
  State<MobileShell> createState() => _MobileShellState();
}

class _MobileShellState extends State<MobileShell> {
  int _index = 0;

  /// Bumped after a booking so the appointments tab reloads instead of showing stale data.
  int _bookingsVersion = 0;

  late final CatalogApi _catalog =
      widget.catalogOverride ?? CatalogApi(widget.auth.api);
  late final ReservationsApi _reservations = ReservationsApi(widget.auth.api);
  late final ShopApi _shop = widget.shopOverride ?? ShopApi(widget.auth.api);
  late final NotificationsApi _notifications = NotificationsApi(
    widget.auth.api,
  );
  int _unread = 0;
  Timer? _poll;
  final CartController _cart = CartController();

  @override
  void initState() {
    super.initState();
    _refreshUnread();
    _poll = Timer.periodic(
      const Duration(seconds: 60),
      (_) => _refreshUnread(),
    );
    // Cards show up once the server has said it can take them.
    widget.cardPayment.prepare().then((_) {
      if (mounted) setState(() {});
    });
  }

  Future<void> _refreshUnread() async {
    try {
      final n = await _notifications.unreadCount();
      if (mounted && n != _unread) setState(() => _unread = n);
    } on Object {
      // The badge is a convenience; a failed poll just keeps the old number.
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    _cart.dispose();
    super.dispose();
  }

  List<_Tab> _tabs(AppLocalizations t) {
    if (widget.auth.user!.isEmployee) {
      return [
        _Tab(
          Icons.calendar_month_outlined,
          (t) => t.appointments,
          () => EmployeeScheduleTab(reservations: _reservations),
        ),
        _Tab(
          Icons.list_alt_outlined,
          (t) => t.services,
          () =>
              PricelistTab(catalog: _catalog, employeeId: widget.auth.user!.id),
        ),
        _Tab(
          Icons.notifications_outlined,
          (t) => t.notifications,
          () => NotificationsPage(
            api: _notifications,
            embedded: true,
            onChanged: _refreshUnread,
          ),
        ),
        _Tab(
          Icons.person_outline,
          (t) => t.profile,
          () => ProfilePage(auth: widget.auth),
        ),
      ];
    }
    return [
      _Tab(
        Icons.home_outlined,
        (t) => t.home,
        () => EmployeesTab(
          catalog: _catalog,
          reservations: _reservations,
          onBooked: () => setState(() => _bookingsVersion++),
        ),
      ),
      _Tab(
        Icons.list_alt_outlined,
        (t) => t.services,
        () => PricelistTab(catalog: _catalog),
      ),
      _Tab(
        Icons.calendar_month_outlined,
        (t) => t.appointments,
        () => MyReservationsTab(
          key: ValueKey(_bookingsVersion),
          reservations: _reservations,
        ),
      ),
      _Tab(
        Icons.shopping_cart_outlined,
        (t) => t.products,
        () =>
            ShopTab(shop: _shop, cart: _cart, cardPayment: widget.cardPayment),
      ),
      _Tab(
        Icons.settings_outlined,
        (t) => t.settings,
        () => ProfilePage(auth: widget.auth),
      ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final tabs = _tabs(t);
    final index = _index.clamp(0, tabs.length - 1);
    final tab = tabs[index];

    return Scaffold(
      appBar: AppBar(
        title: Text(tab.label(t)),
        actions: [
          IconButton(
            key: const Key('open_notifications'),
            tooltip: t.notifications,
            icon: Badge(
              isLabelVisible: _unread > 0,
              label: Text('$_unread'),
              child: const Icon(Icons.notifications_outlined),
            ),
            onPressed: () async {
              await Navigator.of(context).push(
                MaterialPageRoute<void>(
                  builder: (_) => NotificationsPage(
                    api: _notifications,
                    onChanged: _refreshUnread,
                  ),
                ),
              );
              _refreshUnread();
            },
          ),
        ],
      ),
      body: tab.body(),
      bottomNavigationBar: NavigationBar(
        selectedIndex: index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: [
          for (final s in tabs)
            NavigationDestination(icon: Icon(s.icon), label: s.label(t)),
        ],
      ),
    );
  }
}
