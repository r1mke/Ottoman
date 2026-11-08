import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

import 'employees_tab.dart';

/// Step 1 of booking (the "Services" tab): choose one or more services and see the total price and time.
/// Step 2 is the barber (only those who do every chosen service), step 3 the time.
class ServicePickerTab extends StatefulWidget {
  const ServicePickerTab({
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
  State<ServicePickerTab> createState() => _ServicePickerTabState();
}

class _ServicePickerTabState extends State<ServicePickerTab> {
  final _selected = <int>{};

  // Ratings are decoration: a failure must not break the list, so errors become "nothing to show".
  late final Future<Map<String, ReviewSummary>> _ratings = widget.reviews
      .summary(ReviewTarget.service)
      .catchError((_) => <String, ReviewSummary>{});

  void _continue(List<Service> chosen) {
    final t = AppLocalizations.of(context);
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => Scaffold(
          appBar: AppBar(title: Text(t.chooseEmployee)),
          body: EmployeesTab(
            catalog: widget.catalog,
            reservations: widget.reservations,
            reviews: widget.reviews,
            recommendations: widget.recommendations,
            services: chosen,
            onBooked: widget.onBooked,
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return AsyncView<List<Service>>(
      load: widget.catalog.services,
      skeleton: const ListSkeleton(),
      isEmpty: (s) => s.isEmpty,
      builder: (context, services, reload) {
        final chosen = services.where((s) => _selected.contains(s.id)).toList();
        final total = chosen.fold<double>(0, (a, s) => a + s.price);
        final minutes = chosen.fold<int>(0, (a, s) => a + s.durationMinutes);

        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  t.chooseServicesHint,
                  style: TextStyle(
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
                ),
              ),
            ),
            Expanded(
              child: RefreshIndicator(
                onRefresh: reload,
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: services.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final s = services[i];
                    return Card(
                      child: CheckboxListTile(
                        key: Key('service_${s.id}'),
                        secondary: ClipRRect(
                          borderRadius: BorderRadius.circular(10),
                          child: NetworkPicture(
                            url: s.imageUrl,
                            width: 48,
                            height: 48,
                            fallbackIcon: Icons.content_cut,
                          ),
                        ),
                        value: _selected.contains(s.id),
                        onChanged: (v) => setState(
                          () =>
                              v! ? _selected.add(s.id) : _selected.remove(s.id),
                        ),
                        title: Text(
                          s.name,
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        subtitle: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              [
                                if (s.description != null) s.description!,
                                '${t.minutesShort(s.durationMinutes)} · ${s.price.toStringAsFixed(2)} ${t.currencyShort}',
                              ].join('\n'),
                            ),
                            FutureBuilder<Map<String, ReviewSummary>>(
                              future: _ratings,
                              builder: (context, snap) {
                                final r = snap.data?['${s.id}'];
                                if (r == null) return const SizedBox.shrink();
                                return Padding(
                                  key: Key('service_rating_${s.id}'),
                                  padding: const EdgeInsets.only(top: 4),
                                  child: RatingSummary(
                                    average: r.average,
                                    count: r.count,
                                  ),
                                );
                              },
                            ),
                          ],
                        ),
                        isThreeLine: s.description != null,
                      ),
                    );
                  },
                ),
              ),
            ),
            SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (chosen.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Text('${t.total}: ${t.minutesShort(minutes)}'),
                            Text(
                              '${total.toStringAsFixed(2)} ${t.currencyShort}',
                              key: const Key('selection_total'),
                              style: const TextStyle(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ],
                        ),
                      ),
                    FilledButton(
                      key: const Key('continue'),
                      onPressed: chosen.isEmpty
                          ? null
                          : () => _continue(chosen),
                      child: Text(
                        chosen.isEmpty ? t.selectServices : t.continueLabel,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}
