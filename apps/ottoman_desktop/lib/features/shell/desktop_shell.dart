import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../employees/employees_page.dart';
import '../services/services_page.dart';

class _Section {
  const _Section(this.icon, this.label, {this.adminOnly = false, this.body});
  final IconData icon;
  final String Function(AppLocalizations) label;
  final bool adminOnly;

  /// Null renders the "coming soon" placeholder.
  final Widget Function(CatalogApi catalog)? body;
}

/// Left navigation + content area. Sections are filtered by role: staff see only
/// their own schedule and profile, admins see everything.
class DesktopShell extends StatefulWidget {
  const DesktopShell({super.key, required this.auth, this.catalogOverride});

  final AuthController auth;

  /// Test seam; built from the session when null.
  final CatalogApi? catalogOverride;

  @override
  State<DesktopShell> createState() => _DesktopShellState();
}

class _DesktopShellState extends State<DesktopShell> {
  int _index = 0;
  late final CatalogApi _catalog =
      widget.catalogOverride ?? CatalogApi(widget.auth.api);

  static final _sections = <_Section>[
    _Section(Icons.dashboard_outlined, (t) => t.overview, adminOnly: true),
    _Section(Icons.schedule_outlined, (t) => t.appointments),
    _Section(Icons.payments_outlined, (t) => t.earnings, adminOnly: true),
    _Section(
      Icons.list_alt_outlined,
      (t) => t.services,
      adminOnly: true,
      body: (c) => ServicesPage(catalog: c),
    ),
    _Section(
      Icons.notifications_outlined,
      (t) => t.notifications,
      adminOnly: true,
    ),
    _Section(Icons.inventory_2_outlined, (t) => t.products, adminOnly: true),
    _Section(Icons.groups_outlined, (t) => t.clients, adminOnly: true),
    _Section(
      Icons.content_cut,
      (t) => t.employees,
      adminOnly: true,
      body: (c) => EmployeesPage(catalog: c),
    ),
    _Section(Icons.insert_chart_outlined, (t) => t.reports, adminOnly: true),
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
                      current.body?.call(_catalog) ??
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
