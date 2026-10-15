import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../profile/profile_page.dart';

class _Tab {
  const _Tab(this.icon, this.label, {this.isProfile = false});
  final IconData icon;
  final String Function(AppLocalizations) label;
  final bool isProfile;
}

/// Clients and employees get different navigation: clients book and shop,
/// employees only see their schedule, services and notifications.
class MobileShell extends StatefulWidget {
  const MobileShell({super.key, required this.auth});

  final AuthController auth;

  @override
  State<MobileShell> createState() => _MobileShellState();
}

class _MobileShellState extends State<MobileShell> {
  int _index = 0;

  static final _clientTabs = <_Tab>[
    _Tab(Icons.home_outlined, (t) => t.home),
    _Tab(Icons.list_alt_outlined, (t) => t.services),
    _Tab(Icons.calendar_month_outlined, (t) => t.appointments),
    _Tab(Icons.shopping_cart_outlined, (t) => t.products),
    _Tab(Icons.settings_outlined, (t) => t.settings, isProfile: true),
  ];

  static final _employeeTabs = <_Tab>[
    _Tab(Icons.calendar_month_outlined, (t) => t.appointments),
    _Tab(Icons.list_alt_outlined, (t) => t.services),
    _Tab(Icons.notifications_outlined, (t) => t.notifications),
    _Tab(Icons.person_outline, (t) => t.profile, isProfile: true),
  ];

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final tabs = widget.auth.user!.isEmployee ? _employeeTabs : _clientTabs;
    final index = _index.clamp(0, tabs.length - 1);
    final tab = tabs[index];

    return Scaffold(
      appBar: AppBar(title: Text(tab.label(t))),
      body: tab.isProfile
          ? ProfilePage(auth: widget.auth)
          : EmptyState(icon: tab.icon, message: t.comingSoon),
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
