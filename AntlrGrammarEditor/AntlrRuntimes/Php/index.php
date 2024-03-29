<?php

require_once '__RuntimesPath__';
require_once '__TemplateLexerName__.php';
/*$ParserInclude*/require_once '__TemplateParserName__.php';/*ParserInclude$*/

use Antlr\Antlr4\Runtime\CommonTokenStream;
use Antlr\Antlr4\Runtime\InputStream;
use Antlr\Antlr4\Runtime\Error\Listeners\ConsoleErrorListener;
use Antlr\Antlr4\Runtime\Atn\PredictionMode as PredictionMode;
/*$PackageName*/use __PackageName__\__TemplateLexerName__;/*PackageName$*/
/*$PackageNameParser*/use __PackageName__\__TemplateParserName__;/*PackageNameParser$*/

$fileName = "../../Text";

$argvSize = sizeof($argv);
if ($argvSize > 1)
    $fileName = $argv[1];

$input = InputStream::fromPath($fileName);
$lexer = new __TemplateLexerName__($input);
$consoleErrorListener = new ConsoleErrorListener();
$lexer->addErrorListener($consoleErrorListener);
$tokens = new CommonTokenStream($lexer);
/*$ParserPart*/

$rootRule = null;
$mode = "ll";
if ($argvSize > 2) {
    $rootRule = $argv[2];
    if ($argvSize > 3) {
        // TODO: onlyTokenize parameter processing
        if ($argvSize > 4) {
            $mode = strtolower($argv[4]);
        }
    }
}

$parser = new __TemplateParserName__($tokens);
$predictionMode = $mode == "ll"
    ? PredictionMode::LL
    : ($mode == "sll"
        ? PredictionMode::SLL
        : PredictionMode::LL_EXACT_AMBIG_DETECTION);
$parser->getInterpreter()->setPredictionMode($predictionMode);
$parser->addErrorListener($consoleErrorListener);

$ruleName = $rootRule == null ? __TemplateParserName__::RULE_NAMES[0] : $rootRule;
$tree = $parser->{$ruleName}();

print('Tree ' . $tree->toStringTree($parser->getRuleNames()));
/*ParserPart$*/