import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:ottoman_core/ottoman_core.dart';

/// Finds the place of an address (null when nothing matches).
typedef AddressSearch = Future<LatLng?> Function(String address);

/// Looks the address up on the public OpenStreetMap search (Nominatim).
Future<LatLng?> searchOpenStreetMap(String address) async {
  final res = await Dio().get<List<dynamic>>(
    'https://nominatim.openstreetmap.org/search',
    queryParameters: {'q': address, 'format': 'json', 'limit': 1},
    options: Options(headers: {'User-Agent': 'ba.ottoman.ottoman_desktop'}),
  );
  final hits = res.data ?? const [];
  if (hits.isEmpty) return null;
  final hit = hits.first as Map<String, dynamic>;
  final lat = double.tryParse('${hit['lat']}');
  final lon = double.tryParse('${hit['lon']}');
  return lat == null || lon == null ? null : LatLng(lat, lon);
}

/// Admin picks the place of the salon on a map: tap to drop the pin, or type an address and let the map find it.
/// The admin never sees or types raw coordinates.
class LocationPicker extends StatefulWidget {
  const LocationPicker({
    super.key,
    required this.value,
    required this.address,
    required this.onChanged,
    this.search = searchOpenStreetMap,
  });

  final LatLng? value;

  /// The address typed in the form; used as the starting search text.
  final String address;
  final ValueChanged<LatLng?> onChanged;
  final AddressSearch search;

  @override
  State<LocationPicker> createState() => _LocationPickerState();
}

class _LocationPickerState extends State<LocationPicker> {
  // Sarajevo, until the salon has a pin.
  static const _fallback = LatLng(43.8563, 18.4131);
  final _map = MapController();
  late final _query = TextEditingController(text: widget.address);
  bool _searching = false;
  String? _notFound;

  @override
  void dispose() {
    _query.dispose();
    _map.dispose();
    super.dispose();
  }

  Future<void> _find() async {
    final text = _query.text.trim();
    if (text.isEmpty) return;
    final t = AppLocalizations.of(context);
    setState(() {
      _searching = true;
      _notFound = null;
    });
    try {
      final hit = await widget.search(text);
      if (!mounted) return;
      if (hit == null) {
        setState(() => _notFound = t.addressNotFound);
      } else {
        widget.onChanged(hit);
        _map.move(hit, 16);
      }
    } on DioException {
      if (mounted) setState(() => _notFound = t.addressNotFound);
    } finally {
      if (mounted) setState(() => _searching = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context);
    final pin = widget.value;
    final muted = TextStyle(color: Theme.of(context).colorScheme.outline);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          t.salonLocation,
          style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14),
        ),
        const SizedBox(height: 4),
        Text(t.salonLocationHint, style: muted),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: TextField(
                key: const Key('location_query'),
                controller: _query,
                onSubmitted: (_) => _find(),
                decoration: InputDecoration(
                  labelText: t.searchAddress,
                  errorText: _notFound,
                ),
              ),
            ),
            const SizedBox(width: 12),
            FilledButton.icon(
              key: const Key('location_find'),
              onPressed: _searching ? null : _find,
              icon: const Icon(Icons.search, size: 18),
              label: Text(t.findOnMap),
            ),
          ],
        ),
        const SizedBox(height: 12),
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: SizedBox(
            height: 280,
            child: FlutterMap(
              key: const Key('location_map'),
              mapController: _map,
              options: MapOptions(
                initialCenter: pin ?? _fallback,
                initialZoom: pin == null ? 12 : 16,
                onTap: (_, point) => widget.onChanged(point),
              ),
              children: [
                TileLayer(
                  urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
                  userAgentPackageName: 'ba.ottoman.ottoman_desktop',
                ),
                if (pin != null)
                  MarkerLayer(
                    markers: [
                      Marker(
                        point: pin,
                        width: 44,
                        height: 44,
                        alignment: Alignment.topCenter,
                        child: const Icon(
                          Icons.location_on,
                          key: Key('location_pin'),
                          size: 44,
                          color: Color(0xFFD32F2F),
                        ),
                      ),
                    ],
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: Text(
                pin == null ? t.noPinYet : t.pinSet,
                key: const Key('location_state'),
                style: muted,
              ),
            ),
            if (pin != null)
              TextButton(
                key: const Key('location_clear'),
                onPressed: () => widget.onChanged(null),
                child: Text(t.removePin),
              ),
          ],
        ),
      ],
    );
  }
}
