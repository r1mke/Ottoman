import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'employee_services_page.dart';
import 'slot_picker_page.dart';

/// Step 1 of booking: choose a barber (mockup "Uposlenici").
class EmployeesTab extends StatelessWidget {
  const EmployeesTab({
    super.key,
    required this.catalog,
    required this.reservations,
    required this.onBooked,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;

  /// Called after a successful booking so other tabs can refresh.
  final VoidCallback onBooked;

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AsyncView<List<Employee>>(
      load: catalog.employees,
      skeleton: const _EmployeeGridSkeleton(),
      isEmpty: (e) => e.isEmpty,
      builder: (context, employees, reload) => RefreshIndicator(
        onRefresh: reload,
        child: CustomScrollView(
          slivers: [
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      t.chooseEmployee,
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      t.chooseEmployeeHint,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            SliverPadding(
              padding: const EdgeInsets.all(16),
              sliver: SliverGrid.builder(
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 2,
                  mainAxisSpacing: 12,
                  crossAxisSpacing: 12,
                  childAspectRatio: 0.85,
                ),
                itemCount: employees.length,
                itemBuilder: (context, i) => _EmployeeCard(
                  employee: employees[i],
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => EmployeeServicesPage(
                        catalog: catalog,
                        employee: employees[i],
                        onContinue: (ctx, services) => Navigator.of(ctx).push(
                          MaterialPageRoute<void>(
                            builder: (_) => SlotPickerPage(
                              catalog: catalog,
                              reservations: reservations,
                              employee: employees[i],
                              services: services,
                              onBooked: onBooked,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EmployeeCard extends StatelessWidget {
  const _EmployeeCard({required this.employee, required this.onTap});

  final Employee employee;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        key: Key('employee_${employee.id}'),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              CircleAvatar(
                radius: 40,
                backgroundColor: scheme.surfaceContainerHighest,
                child: Text(
                  '${employee.firstName.characters.first}${employee.lastName.characters.first}',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              const SizedBox(height: 12),
              Text(
                employee.fullName,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 16,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                employee.title,
                style: TextStyle(color: scheme.onSurfaceVariant, fontSize: 12),
              ),
              if (employee.bio != null) ...[
                const SizedBox(height: 6),
                Text(
                  employee.bio!,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontStyle: FontStyle.italic,
                    fontSize: 12,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _EmployeeGridSkeleton extends StatelessWidget {
  const _EmployeeGridSkeleton();

  @override
  Widget build(BuildContext context) => GridView.count(
    physics: const NeverScrollableScrollPhysics(),
    padding: const EdgeInsets.all(16),
    crossAxisCount: 2,
    mainAxisSpacing: 12,
    crossAxisSpacing: 12,
    childAspectRatio: 0.85,
    children: List.generate(4, (_) => const SkeletonBox(radius: 12)),
  );
}
