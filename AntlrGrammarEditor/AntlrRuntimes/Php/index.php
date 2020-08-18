<?php

require_once '__RuntimesPath__';
require_once '__TemplateGrammarName__Lexer.php';
/*$ParserInclude*/require_once '__TemplateGrammarName__Parser.php';/*ParserInclude$*/
/*AntlrCaseInsensitive*/

use Antlr\Antlr4\Runtime\CommonTokenStream;
use Antlr\Antlr4\Runtime\InputStream;
use Antlr\Antlr4\Runtime\Error\Listeners\ConsoleErrorListener;
/*$PackageName$*/

$fileName = "../../Text";

if (sizeof($argv) > 1)
    $fileName = $argv[1];

$input = InputStream::fromPath($fileName);
$lexer = new __TemplateGrammarName__Lexer($input);
$consoleErrorListener = new ConsoleErrorListener();
$lexer->addErrorListener($consoleErrorListener);
$tokens = new CommonTokenStream($lexer);
/*$ParserPart*/
$parser = new __TemplateGrammarName__Parser($tokens);
$parser->addErrorListener($consoleErrorListener);

$rootRule = null;
if (sizeof($argv) > 2)
    $rootRule = $argv[2];

$ruleName = $rootRule == null ? __TemplateGrammarName__Parser::RULE_NAMES[0] : $rootRule;
$tree = $parser->{$ruleName}();

print('Tree ' . $tree->toStringTree($parser->getRuleNames()));
/*ParserPart$*/