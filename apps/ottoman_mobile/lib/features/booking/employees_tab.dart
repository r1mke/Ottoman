import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'employee_services_page.dart';
import 'slot_picker_page.dart';

/// Step 1 of booking: choose a barber (mockup "Uposlenici").
class EmployeesTab extends StatefulWidget {
  const EmployeesTab({
    super.key,
    required this.catalog,
    required this.reservations,
    required this.reviews,
    required this.recommendations,
    required this.onBooked,
  });

  final CatalogApi catalog;
  final ReservationsApi reservations;
  final ReviewsApi reviews;
  final RecommendationsApi recommendations;

  /// Called after a successful booking so other tabs can refresh.
  final VoidCallback onBooked;

  @override
  State<EmployeesTab> createState() => _EmployeesTabState();
}

class _EmployeesTabState extends State<EmployeesTab> {
  CatalogApi get catalog => widget.catalog;
  ReservationsApi get reservations => widget.reservations;
  VoidCallback get onBooked => widget.onBooked;

  // Ratings and suggestions are decoration: a failure must not break the list, so errors become "nothing to show".
  late final Future<Map<String, ReviewSummary>> _ratings = widget.reviews
      .summary(ReviewTarget.employee)
      .catchError((_) => <String, ReviewSummary>{});
  late final Future<List<Recommended<Employee>>> _recommended = widget
      .recommendations
      .employees()
      .catchError((_) => <Recommended<Employee>>[]);

  void _open(BuildContext context, Employee employee) =>
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => EmployeeServicesPage(
            catalog: catalog,
            employee: employee,
            onContinue: (ctx, services) => Navigator.of(ctx).push(
              MaterialPageRoute<void>(
                builder: (_) => SlotPickerPage(
                  catalog: catalog,
                  reservations: reservations,
                  employee: employee,
                  services: services,
                  onBooked: onBooked,
                ),
              ),
            ),
          ),
        ),
      );

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
            SliverToBoxAdapter(
              child: FutureBuilder<List<Recommended<Employee>>>(
                future: _recommended,
                builder: (context, snap) {
                  final picks = snap.data;
                  if (picks == null || picks.isEmpty) {
                    return const SizedBox.shrink();
                  }
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Padding(
                        padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
                        child: Text(
                          t.recommendedForYou,
                          style: Theme.of(context).textTheme.titleMedium
                              ?.copyWith(fontWeight: FontWeight.w700),
                        ),
                      ),
                      SizedBox(
                        height: 104,
                        child: ListView(
                          scrollDirection: Axis.horizontal,
                          padding: const EdgeInsets.symmetric(horizontal: 16),
                          children: [
                            for (final pick in picks)
                              Padding(
                                padding: const EdgeInsets.only(right: 10),
                                child: SizedBox(
                                  width: 240,
                                  child: Card(
                                    key: Key('reco_employee_${pick.item.id}'),
                                    clipBehavior: Clip.antiAlias,
                                    child: InkWell(
                                      onTap: () => _open(context, pick.item),
                                      child: Padding(
                                        padding: const EdgeInsets.all(12),
                                        child: Column(
                                          crossAxisAlignment:
                                              CrossAxisAlignment.start,
                                          children: [
                                            Row(
                                              children: [
                                                const Icon(
                                                  Icons.content_cut,
                                                  size: 16,
                                                ),
                                                const SizedBox(width: 6),
                                                Expanded(
                                                  child: Text(
                                                    pick.item.fullName,
                                                    maxLines: 1,
                                                    overflow:
                                                        TextOverflow.ellipsis,
                                                    style: const TextStyle(
                                                      fontWeight:
                                                          FontWeight.w700,
                                                    ),
                                                  ),
                                                ),
                                              ],
                                            ),
                                            const SizedBox(height: 6),
                                            Text(
                                              pick.reason,
                                              key: Key(
                                                'reco_reason_${pick.item.id}',
                                              ),
                                              maxLines: 3,
                                              overflow: TextOverflow.ellipsis,
                                              style: TextStyle(
                                                color: Theme.of(context)
                                                    .colorScheme
                                                    .primary,
                                                fontSize: 12,
                                                fontStyle: FontStyle.italic,
                                              ),
                                            ),
                                          ],
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ],
                  );
                },
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
                itemBuilder: (context, i) =>
                    FutureBuilder<Map<String, ReviewSummary>>(
                      future: _ratings,
                      builder: (context, snap) => _EmployeeCard(
                        employee: employees[i],
                        rating: snap.data?[employees[i].id],
                        onTap: () => _open(context, employees[i]),
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
  const _EmployeeCard({
    required this.employee,
    required this.onTap,
    this.rating,
  });

  final Employee employee;
  final ReviewSummary? rating;
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
              PersonAvatar(
                radius: 40,
                initials: employee.initials,
                photoUrl: employee.photoUrl,
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
              if (rating != null)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: RatingSummary(
                    average: rating!.average,
                    count: rating!.count,
                  ),
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
