<?php

require_once '__RuntimesPath__';
require_once '__TemplateGrammarName__Lexer.php';
/*$ParserInclude*/require_once '__TemplateGrammarName__Parser.php';/*ParserInclude$*/
/*AntlrCaseInsensitive*/

use Antlr\Antlr4\Runtime\CommonTokenStream;
use Antlr\Antlr4\Runtime\InputStream;
use Antlr\Antlr4\Runtime\Error\Listeners\ConsoleErrorListener;
use Antlr\Antlr4\Runtime\Atn\PredictionMode as PredictionMode;
/*$PackageName$*/

$fileName = "../../Text";

$argvSize = sizeof($argv);
if ($argvSize > 1)
    $fileName = $argv[1];

$input = InputStream::fromPath($fileName);
$lexer = new __TemplateGrammarName__Lexer($input);
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

$parser = new __TemplateGrammarName__Parser($tokens);
$predictionMode = $mode == "ll"
    ? PredictionMode::LL
    : ($mode == "sll"
        ? PredictionMode::SLL
        : PredictionMode::LL_EXACT_AMBIG_DETECTION);
$parser->getInterpreter()->setPredictionMode($predictionMode);
$parser->addErrorListener($consoleErrorListener);

$ruleName = $rootRule == null ? __TemplateGrammarName__Parser::RULE_NAMES[0] : $rootRule;
$tree = $parser->{$ruleName}();

print('Tree ' . $tree->toStringTree($parser->getRuleNames()));
/*ParserPart$*/