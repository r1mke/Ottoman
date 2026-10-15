import 'package:flutter/material.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Step 2 of booking: choose one or more of this barber's services.
/// The slot picker and confirmation (step 3) are added in the reservations phase.
class EmployeeServicesPage extends StatefulWidget {
  const EmployeeServicesPage({
    super.key,
    required this.catalog,
    required this.employee,
    this.onContinue,
  });

  final CatalogApi catalog;
  final Employee employee;

  /// Receives the chosen services. Null until the reservation flow is wired in.
  final void Function(BuildContext context, List<Service> services)? onContinue;

  @override
  State<EmployeeServicesPage> createState() => _EmployeeServicesPageState();
}

class _EmployeeServicesPageState extends State<EmployeeServicesPage> {
  final _selected = <int>{};

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(widget.employee.fullName)),
      body: AsyncView<List<Service>>(
        load: () => widget.catalog.services(employeeId: widget.employee.id),
        skeleton: const ListSkeleton(),
        isEmpty: (s) => s.isEmpty,
        builder: (context, services, reload) {
          final chosen = services
              .where((s) => _selected.contains(s.id))
              .toList();
          final total = chosen.fold<double>(0, (a, s) => a + s.price);
          final minutes = chosen.fold<int>(0, (a, s) => a + s.durationMinutes);

          return Column(
            children: [
              Expanded(
                child: RefreshIndicator(
                  onRefresh: reload,
                  child: ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: services.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, i) {
                      final s = services[i];
                      final on = _selected.contains(s.id);
                      return Card(
                        child: CheckboxListTile(
                          key: Key('service_${s.id}'),
                          value: on,
                          onChanged: (v) => setState(
                            () => v!
                                ? _selected.add(s.id)
                                : _selected.remove(s.id),
                          ),
                          title: Text(
                            s.name,
                            style: const TextStyle(fontWeight: FontWeight.w700),
                          ),
                          subtitle: Text(
                            '${t.minutesShort(s.durationMinutes)} · ${s.price.toStringAsFixed(2)} ${t.currencyShort}',
                          ),
                        ),
                      );
                    },
                  ),
                ),
              ),
              SafeArea(
                child: Padding(
                  padding: const EdgeInsets.all(16),
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
                        onPressed: chosen.isEmpty || widget.onContinue == null
                            ? null
                            : () => widget.onContinue!(context, chosen),
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
      ),
    );
  }
}
