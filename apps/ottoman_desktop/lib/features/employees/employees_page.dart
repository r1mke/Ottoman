import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import '../../widgets/account_lock.dart';
import '../../widgets/list_search_field.dart';
import 'employee_dialogs.dart';

/// Admin: staff list with edit, service assignment and working-hours actions.
class EmployeesPage extends StatefulWidget {
  const EmployeesPage({super.key, required this.catalog});

  final CatalogApi catalog;

  @override
  State<EmployeesPage> createState() => _EmployeesPageState();
}

class _EmployeesPageState extends State<EmployeesPage> {
  int _version = 0;
  String _query = '';

  void _refresh() => setState(() => _version++);

  Future<void> _open(Widget dialog) async {
    await showDialog<bool>(context: context, builder: (_) => dialog);
    // A picture change is saved immediately, so refresh even when the dialog was only closed.
    if (mounted) _refresh();
  }

  Future<void> _unlock(Employee e) async {
    final unlocked = await unlockAccount(
      context,
      name: e.fullName,
      unlock: () => widget.catalog.unlockEmployee(e.id),
    );
    if (unlocked && mounted) _refresh();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(32, 0, 32, 24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ListSearchField(
                fieldKey: const Key('employee_search'),
                hint: t.employeeSearchHint,
                onSearch: (q) => setState(() => _query = q),
              ),
              const Spacer(),
              FilledButton.icon(
                key: const Key('add_employee'),
                style: FilledButton.styleFrom(minimumSize: const Size(140, 44)),
                onPressed: () => _open(EmployeeDialog(catalog: widget.catalog)),
                icon: const Icon(Icons.add),
                label: Text(t.add),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Expanded(
            child: AsyncView<List<Employee>>(
              key: ValueKey('$_version-$_query'),
              load: () => widget.catalog.employees(
                includeInactive: true,
                search: _query,
              ),
              skeleton: const ListSkeleton(),
              isEmpty: (e) => e.isEmpty,
              builder: (context, employees, _) => ListView.separated(
                itemCount: employees.length,
                separatorBuilder: (_, _) => const SizedBox(height: 8),
                itemBuilder: (context, i) {
                  final e = employees[i];
                  return Card(
                    child: ListTile(
                      key: Key('employee_row_${e.id}'),
                      leading: PersonAvatar(
                        photoUrl: e.photoUrl,
                        gender: e.gender,
                        seed: e.id,
                      ),
                      title: Text(
                        e.fullName,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                      subtitle: Text('${e.title} · ${e.email ?? ''}'),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          if (e.lockedUntilUtc != null) ...[
                            LockedChip(
                              key: Key('employee_locked_${e.id}'),
                              until: e.lockedUntilUtc!,
                            ),
                            const SizedBox(width: 6),
                            UnlockButton(
                              key: Key('employee_unlock_${e.id}'),
                              onPressed: () => _unlock(e),
                            ),
                            const SizedBox(width: 6),
                          ],
                          Chip(label: Text(e.isActive ? t.active : t.inactive)),
                          IconButton(
                            tooltip: t.edit,
                            icon: const Icon(Icons.edit_outlined),
                            onPressed: () => _open(
                              EmployeeDialog(
                                catalog: widget.catalog,
                                employee: e,
                              ),
                            ),
                          ),
                          IconButton(
                            key: Key('assign_btn_${e.id}'),
                            tooltip: t.services,
                            icon: const Icon(Icons.list_alt_outlined),
                            onPressed: () => _open(
                              AssignServicesDialog(
                                catalog: widget.catalog,
                                employee: e,
                              ),
                            ),
                          ),
                          IconButton(
                            tooltip: t.workingHours,
                            icon: const Icon(Icons.schedule_outlined),
                            onPressed: () => _open(
                              WorkingHoursDialog(
                                catalog: widget.catalog,
                                employee: e,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
