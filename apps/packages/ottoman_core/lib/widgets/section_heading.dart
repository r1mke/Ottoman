import 'package:flutter/material.dart';

/// A block heading the way the mobile app does it everywhere: a small amber caps label, a large title and a short
/// amber bar underneath. [centered] puts all three in the middle; [trailing] sits at the right end of the title line.
class SectionHeading extends StatelessWidget {
  const SectionHeading(
    this.text, {
    super.key,
    this.eyebrow,
    this.top = 36,
    this.centered = false,
    this.trailing,
  });

  final String text;

  /// The small amber text above the title; left out when null or empty.
  final String? eyebrow;

  /// Space above the heading.
  final double top;

  /// Centre the heading (the admin's choice for a section).
  final bool centered;

  /// Something at the right end of the title line, such as an "all products" link.
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final align = centered
        ? CrossAxisAlignment.center
        : CrossAxisAlignment.start;
    final title = Text(
      text,
      textAlign: centered ? TextAlign.center : TextAlign.start,
      style: theme.textTheme.headlineMedium?.copyWith(
        fontWeight: FontWeight.w800,
        height: 1.1,
      ),
    );
    return Padding(
      padding: EdgeInsets.fromLTRB(20, top, 20, 16),
      child: Column(
        crossAxisAlignment: align,
        children: [
          if (eyebrow != null && eyebrow!.trim().isNotEmpty) ...[
            Text(
              eyebrow!.toUpperCase(),
              textAlign: centered ? TextAlign.center : TextAlign.start,
              style: TextStyle(
                color: theme.colorScheme.primary,
                fontWeight: FontWeight.w800,
                fontSize: 14,
                letterSpacing: 0.6,
              ),
            ),
            const SizedBox(height: 4),
          ],
          if (trailing == null || centered)
            title
          else
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Expanded(child: title),
                trailing!,
              ],
            ),
          const SizedBox(height: 10),
          Container(
            width: 56,
            height: 5,
            decoration: BoxDecoration(
              color: theme.colorScheme.primary,
              borderRadius: BorderRadius.circular(3),
            ),
          ),
        ],
      ),
    );
  }
}
