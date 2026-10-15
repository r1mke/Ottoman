import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../booking/employees_tab.dart';
import '../profile/profile_page.dart';
import '../services/pricelist_tab.dart';

class _Tab {
  const _Tab(this.icon, this.label, this.body);
  final IconData icon;
  final String Function(AppLocalizations) label;
  final Widget Function(MobileShell shell) body;
}

/// Clients and employees get different navigation: clients book and shop,
/// employees only see their schedule, services and notifications.
class MobileShell extends StatefulWidget {
  const MobileShell({super.key, required this.auth, this.catalogOverride});

  final AuthController auth;

  /// Test seam; the shell builds its own [CatalogApi] from the session when null.
  final CatalogApi? catalogOverride;

  @override
  State<MobileShell> createState() => _MobileShellState();
}

class _MobileShellState extends State<MobileShell> {
  int _index = 0;
  late final CatalogApi _catalog =
      widget.catalogOverride ?? CatalogApi(widget.auth.api);

  Widget _soon(IconData icon, AppLocalizations t) =>
      EmptyState(icon: icon, message: t.comingSoon);

  List<_Tab> _tabs(AppLocalizations t) {
    if (widget.auth.user!.isEmployee) {
      return [
        _Tab(
          Icons.calendar_month_outlined,
          (t) => t.appointments,
          (_) => _soon(Icons.calendar_month_outlined, t),
        ),
        _Tab(
          Icons.list_alt_outlined,
          (t) => t.services,
          (s) =>
              PricelistTab(catalog: _catalog, employeeId: widget.auth.user!.id),
        ),
        _Tab(
          Icons.notifications_outlined,
          (t) => t.notifications,
          (_) => _soon(Icons.notifications_outlined, t),
        ),
        _Tab(
          Icons.person_outline,
          (t) => t.profile,
          (s) => ProfilePage(auth: widget.auth),
        ),
      ];
    }
    return [
      _Tab(
        Icons.home_outlined,
        (t) => t.home,
        (_) => EmployeesTab(catalog: _catalog),
      ),
      _Tab(
        Icons.list_alt_outlined,
        (t) => t.services,
        (_) => PricelistTab(catalog: _catalog),
      ),
      _Tab(
        Icons.calendar_month_outlined,
        (t) => t.appointments,
        (_) => _soon(Icons.calendar_month_outlined, t),
      ),
      _Tab(
        Icons.shopping_cart_outlined,
        (t) => t.products,
        (_) => _soon(Icons.shopping_cart_outlined, t),
      ),
      _Tab(
        Icons.settings_outlined,
        (t) => t.settings,
        (s) => ProfilePage(auth: widget.auth),
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
      appBar: AppBar(title: Text(tab.label(t))),
      body: tab.body(widget),
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
