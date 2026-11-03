import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

class SidebarItem {
  const SidebarItem({
    required this.icon,
    required this.label,
    this.badge = 0,
    this.group,
  });

  final IconData icon;
  final String label;

  /// Heading shown above the first entry of each run of equal groups; null = no heading.
  final String? group;

  /// Count shown as an amber pill; zero hides it.
  final int badge;
}

/// The dark navigation column of the desktop app (variant B).
class Sidebar extends StatelessWidget {
  const Sidebar({
    super.key,
    required this.items,
    required this.selected,
    required this.onSelect,
    required this.userName,
    required this.onLogout,
  });

  static const width = 248.0;

  final List<SidebarItem> items;
  final int selected;
  final ValueChanged<int> onSelect;
  final String userName;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Container(
      width: width,
      color: WorkspaceColors.sidebar,
      padding: const EdgeInsets.fromLTRB(14, 24, 14, 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 24),
            child: Row(
              children: [
                const OttomanLogo(
                  key: Key('sidebar_logo'),
                  variant: LogoVariant.horizontal,
                  light: true,
                  height: 40,
                ),
              ],
            ),
          ),
          Expanded(
            child: ListView(
              children: [
                for (var i = 0; i < items.length; i++) ...[
                  if (items[i].group != null &&
                      (i == 0 || items[i].group != items[i - 1].group))
                    _GroupHeading(
                      key: Key('nav_group_${items[i].group}'),
                      label: items[i].group!,
                      first: i == 0,
                    ),
                  _Entry(
                    key: Key('nav_$i'),
                    item: items[i],
                    selected: i == selected,
                    onTap: () => onSelect(i),
                  ),
                ],
              ],
            ),
          ),
          const Divider(color: WorkspaceColors.sidebarActive, height: 1),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 10, 0, 0),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    userName,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: WorkspaceColors.sidebarMuted,
                      fontSize: 13,
                    ),
                  ),
                ),
                IconButton(
                  key: const Key('logout'),
                  tooltip: t.logout,
                  color: WorkspaceColors.sidebarText,
                  icon: const Icon(Icons.logout, size: 20),
                  onPressed: onLogout,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _GroupHeading extends StatelessWidget {
  const _GroupHeading({super.key, required this.label, required this.first});

  final String label;
  final bool first;

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.fromLTRB(12, first ? 0 : 18, 12, 6),
    child: Text(
      label.toUpperCase(),
      style: const TextStyle(
        color: WorkspaceColors.sidebarMuted,
        fontSize: 11,
        fontWeight: FontWeight.w800,
        letterSpacing: 1.2,
      ),
    ),
  );
}

class _Entry extends StatelessWidget {
  const _Entry({
    super.key,
    required this.item,
    required this.selected,
    required this.onTap,
  });

  final SidebarItem item;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? Colors.white : WorkspaceColors.sidebarText;
    return Padding(
      padding: const EdgeInsets.only(bottom: 2),
      child: Material(
        color: selected ? WorkspaceColors.sidebarActive : Colors.transparent,
        borderRadius: BorderRadius.circular(8),
        child: InkWell(
          borderRadius: BorderRadius.circular(8),
          hoverColor: WorkspaceColors.sidebarActive.withValues(alpha: 0.6),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            child: Row(
              children: [
                Icon(item.icon, size: 18, color: color),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    item.label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: color,
                      fontWeight: FontWeight.w600,
                      fontSize: 14,
                    ),
                  ),
                ),
                if (item.badge > 0)
                  Container(
                    key: const Key('nav_badge'),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 7,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: WorkspaceColors.amber,
                      borderRadius: BorderRadius.circular(99),
                    ),
                    child: Text(
                      item.badge > 99 ? '99+' : '${item.badge}',
                      style: const TextStyle(
                        color: WorkspaceColors.ink,
                        fontSize: 11,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
