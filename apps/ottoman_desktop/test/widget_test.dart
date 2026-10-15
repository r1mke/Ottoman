import 'package:flutter_test/flutter_test.dart';
import 'package:ottoman_desktop/main.dart';

void main() {
  testWidgets('shows the app name', (tester) async {
    await tester.pumpWidget(const OttomanApp());
    await tester.pumpAndSettle();
    expect(find.text('OTTOMAN'), findsOneWidget);
  });
}
