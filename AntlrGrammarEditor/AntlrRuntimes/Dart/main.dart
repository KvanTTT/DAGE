/*$PackageName*/library __PackageName__;/*PackageName$*/

import 'dart:mirrors';
import 'package:antlr4/antlr4.dart';

/*$LexerInclude*/import '__TemplateLexerName__.dart';/*LexerInclude$*/
/*$ParserInclude*/import '__TemplateParserName__.dart';/*ParserInclude$*/

/*$Part$*/

void main(List<String> arguments) async {
  __TemplateLexerName__.checkVersion();
  var fileName = "../../Text";

  if (arguments.length > 0) {
    fileName = arguments[0];
  }

  final input = await InputStream.fromPath(fileName);
  final lexer = __TemplateLexerName__(input);
  final tokens = CommonTokenStream(lexer);

/*$ParserPart*/
  var rootRule = "";
  var mode = "ll";
  if (arguments.length > 1) {
    rootRule = arguments[1];
    if (arguments.length > 2) {
      // TODO: onlyTokenize parameter
      if (arguments.length > 3) {
        mode = arguments[3].toLowerCase();
      }
    }
  }

  __TemplateParserName__.checkVersion();
  final parser = __TemplateParserName__(tokens);
  parser.buildParseTree = true;
  parser.interpreter?.predictionMode = mode == "sll"
      ? PredictionMode.SLL
      : mode == "ll"
      ? PredictionMode.LL
      : PredictionMode.LL_EXACT_AMBIG_DETECTION;
  final reflectedParser = reflect(parser);
  if (rootRule == "") rootRule = parser.ruleNames[0];
  final tree = reflectedParser.invoke(Symbol(rootRule), []).reflectee;
  print("Tree " + tree.toStringTree(ruleNames: parser.ruleNames));
/*ParserPart$*/
}
