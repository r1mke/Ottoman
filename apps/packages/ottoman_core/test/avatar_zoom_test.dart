import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_core/ottoman_core.dart';

const _url =
    'https://res.cloudinary.com/demo/image/upload/c_thumb,g_face,w_512,h_512,z_1,q_auto,f_auto/ottoman/employees/a';

Future<void> _pump(
  WidgetTester tester, {
  double zoom = 1.0,
  required ValueChanged<double> onChangeEnd,
}) => tester.pumpWidget(
  MaterialApp(
    locale: const Locale('bs'),
    localizationsDelegates: AppLocalizations.localizationsDelegates,
    supportedLocales: AppLocalizations.supportedLocales,
    home: Scaffold(
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: AvatarZoomEditor(
          photoUrl: _url,
          zoom: zoom,
          gender: 'Female',
          seed: 'e1',
          onChangeEnd: onChangeEnd,
        ),
      ),
    ),
  ),
);

void main() {
  group('AvatarZoom.withZoom', () {
    test(
      'swaps the crop value of the server URL for the inverse of the zoom',
      () {
        expect(AvatarZoom.withZoom(_url, 2.0), contains(',z_0.5,'));
        expect(AvatarZoom.withZoom(_url, 1.6), contains(',z_0.63,'));
        expect(AvatarZoom.withZoom(_url, 1.0), contains(',z_1,'));
        // Everything else in the URL stays as it was.
        expect(
          AvatarZoom.withZoom(_url, 2.0),
          _url.replaceFirst('z_1', 'z_0.5'),
        );
      },
    );

    test(
      'is clamped to the allowed range and leaves a URL without a crop alone',
      () {
        expect(AvatarZoom.withZoom(_url, 9), contains(',z_0.5,'));
        expect(AvatarZoom.withZoom(_url, 0.1), contains(',z_1,'));
        expect(
          AvatarZoom.withZoom('https://x.test/a.png', 1.5),
          'https://x.test/a.png',
        );
      },
    );
  });

  testWidgets(
    'releasing the slider reports the chosen zoom, dragging does not',
    (tester) async {
      final chosen = <double>[];
      await _pump(tester, onChangeEnd: chosen.add);
      expect(find.text('1.0×'), findsOneWidget);

      final slider = tester.getCenter(
        find.byKey(const Key('avatar_zoom_slider')),
      );
      final gesture = await tester.startGesture(slider);
      await gesture.moveBy(const Offset(40, 0));
      await tester.pump();
      expect(chosen, isEmpty);
      expect(find.text('1.0×'), findsNothing); // the label follows the finger
      await gesture.up();
      await tester.pump();

      expect(chosen, hasLength(1));
      expect(chosen.single, inInclusiveRange(1.0, 2.0));
    },
  );

  testWidgets('the preview uses the zoom the owner passes in', (tester) async {
    await _pump(tester, zoom: 2.0, onChangeEnd: (_) {});

    final avatar = tester.widget<PersonAvatar>(
      find.byKey(const Key('avatar_zoom_preview')),
    );
    expect(avatar.photoUrl, contains(',z_0.5,'));
    expect(find.text('2.0×'), findsOneWidget);
  });

  testWidgets('reset goes back to 1 and is disabled at 1', (tester) async {
    final chosen = <double>[];
    await _pump(tester, zoom: 1.0, onChangeEnd: chosen.add);
    expect(
      tester
          .widget<TextButton>(find.byKey(const Key('avatar_zoom_reset')))
          .onPressed,
      isNull,
    );

    await _pump(tester, zoom: 1.8, onChangeEnd: chosen.add);
    await tester.tap(find.byKey(const Key('avatar_zoom_reset')));
    await tester.pump();

    expect(chosen, [1.0]);
  });
}
