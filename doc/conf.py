# Configuration file for the Sphinx documentation builder.
#
# This file only contains a selection of the most common options. For a full
# list see the documentation:
# https://www.sphinx-doc.org/en/master/usage/configuration.html

# -- Path setup --------------------------------------------------------------

# If extensions (or modules to document with autodoc) are in another directory,
# add these directories to sys.path here. If the directory is relative to the
# documentation root, use os.path.abspath to make it absolute, like shown here.
#
import os
import sys
sys.path.append(os.path.abspath('./_ext'))

import re
import json
import guzzle_sphinx_theme

from docutils.parsers.rst import Directive
from docutils import nodes


# -- Project information -----------------------------------------------------

project = 'Lambda Factory'
copyright = '2023, Kyle Stemen'
author = 'Kyle Stemen'


# -- General configuration ---------------------------------------------------

# Add any Sphinx extension module names here, as strings. They can be
# extensions coming with Sphinx (named 'sphinx.ext.*') or your custom
# ones.
extensions = [
    'sphinx_design',
    'myst_parser',
    'sphinx.ext.todo',
    'image_grid',
]

todo_include_todos = True

myst_enable_extensions = [
    'attrs_image',
]

chtml = dict()

if tags.has('offline'):
    # To use offline mode, invoke sphinx-autobuild with -t offline:
    #   sphinx-autobuild -t offline . _build/
    #
    # If using sphinx-mathjax-offline, please ensure it has MathJax version 4.0
    # or higher.
    extensions.append('sphinx-mathjax-offline')
    # This is the location where I added the font in my custom install of
    # sphinx-mathjax-offline.
    chtml['fontPath'] = '_static/mathjax/%%FONT%%-font/es5'

# Add any paths that contain templates here, relative to this directory.
templates_path = ['_templates']

# List of patterns, relative to source directory, that match files and
# directories to ignore when looking for source files.
# This pattern also affects html_static_path and html_extra_path.
exclude_patterns = ['_build', 'Thumbs.db', '.DS_Store']

# -- Options for Latex output ------------------------------------------------
# Xelatex or Lualatex is necessary for the unicode-math package. Xelatex has
# bad kerning.
latex_engine = 'lualatex'
latex_elements = {
    'passoptionstopackages': r'\PassOptionsToPackage{svgnames}{xcolor}',
    'preamble': r'''
\usepackage{amsmath}
\usepackage{mathtools}
\usepackage{unicode-math}
\usepackage{fontsetup}
\usepackage{bussproofs}
\usepackage{custommath}
\usepackage{stmaryrd}

\makeatletter
\@namedef{DUroleinference-title}#1{\sphinxstylestrong{\color{DarkBlue} #1}}
\makeatother
''',
    'extraclassoptions': 'openany,oneside',
}
latex_additional_files = ['custommath.sty']


# -- Options for HTML output -------------------------------------------------

html_theme_path = guzzle_sphinx_theme.html_theme_path()
html_theme = 'guzzle_sphinx_theme'

html_theme_options = {
    # Set the name of the project to appear at the top of the left sidebar
    "project_nav_name": "Lambda factory",
}
html_sidebars = {
   '**': ['logo-text.html', 'globaltoc.html', 'searchbox.html'],
}

# Add any paths that contain custom static files (such as style sheets) here,
# relative to this directory. They are copied after the builtin static files,
# so a file named "default.css" will overwrite the builtin "default.css".
html_static_path = ['_static']

html_css_files = [
    'custom.css',
]

# Initialize the macros dict with definitions of the unicode-math commands that
# MathJax is missing. This is different from custommath.sty, which contains
# macros that should be defined for both MathJax and LaTeX.
macros = {
    'smwhtsquare': '\u25ab',
    # medium small white square would normally be defined as U+25FD, except on
    # Linux it is rendered as a light gray square, instead of a white square
    # with a black outline. So instead use the medium white square and scale it
    # down.
    'mdsmwhtsquare': ('\\style{font-size: 65%; line-height: 1em; '
                      'vertical-align:0.35ex;}{\u25fb}'),
}
pairedDelimiters = { }
matched_braces_regex = '[^{}]*'
# Allow up to this many nested braces within the parsed commands.
max_nesting = 5
for i in range(max_nesting):
    matched_braces_regex = '(?:{' + matched_braces_regex + '}|[^{}])*'
# Parse the macro definitions out of custommath.sty and provide them to MathJax.
with open('custommath.sty', 'r') as f:
    # Remove the LaTex comments from the file.
    contents = "\n".join(line.partition('%')[0]
                         for line in f.read().splitlines())
    for match in re.finditer(
            # start of a Latex command used to declare a new command
            r'\\(?P<decl>DeclareRobustCommand|newcommand|DeclareMathOperator)'
            # the name of the command being defined, optionally surrounded by
            # braces
            r'(?:{\\(?P<cmd_name1>[^{}]+)}|\\(?P<cmd_name2>[^{}]+))'
            # optionally, the number of arguments for the command
            r'(?:\[(?P<params>\d+)\])?'
            # the replacement text for the new command -- only `max_nesting`
            # additional levels of braces nesting are supported.
            fr'{{(?P<definien>{matched_braces_regex})}}'

            # start of a paired delimiter definition
            r'|\\DeclarePairedDelimiter'
            # the name of the command being defined, optionally surrounded by
            # braces
            r'(?:{\\(?P<delim_name1>[^{}]+)}|\\(?P<delim_name2>[^{}]+))'
            # the left delimiter
            r'{(?P<left>[^{}]*)}'
            # the right delimiter
            r'{(?P<right>[^{}]*)}'
            ,
            contents):
        cmd_name = match.group('cmd_name1') or match.group('cmd_name2')
        delim_name = match.group('delim_name1') or match.group('delim_name2')
        if cmd_name:
            decl = match.group('decl')
            definien = match.group('definien')
            definien = match.group('definien')
            if decl == 'DeclareMathOperator':
                definien = '\mathop{\mathrm{%s}}' % definien
            if match.group('params'):
                macros[cmd_name] = [
                    definien,
                    int(match.group('params'))
                ]
            else:
                macros[cmd_name] = definien
        elif delim_name:
            pairedDelimiters[delim_name] = [match.group('left'),
                                            match.group('right')]

# A MathJax start up function is necessary to register more delimiters (from
# https://github.com/mathjax/MathJax/issues/2535). However, Sphinx doesn't
# support including functions in mathjax3_config. So instead, encode the
# MathJax options in a small Javascript script, and inline that script on every
# page. This is based on
# https://github.com/cpitclaudel/alectryon/blob/master/recipes/sphinx/conf.py.

mathjax_config_script = r'''
window.MathJax = {
    startup: {
        ready() {
            MathJax.startup.defaultReady();
            const {DelimiterMap} = MathJax._.input.tex.SymbolMap;
            const {Symbol} = MathJax._.input.tex.Symbol;
            const {MapHandler} = MathJax._.input.tex.MapHandler;
            const delimiter = MapHandler.getMap('delimiter');
            delimiter.add('\\lBrack', new Symbol('\\lBrack', '\u27E6'));
            delimiter.add('\\rBrack', new Symbol('\\rBrack', '\u27E7'));
            delimiter.add('\\lbrackubar', new Symbol('\\lbrackubar', '\u298B'));
            delimiter.add('\\rbrackubar', new Symbol('\\rbrackubar', '\u298C'));
            delimiter.add('\\lbracklltick', new Symbol('\\lbracklltick', '\u298F'));
            delimiter.add('\\rbracklrtick', new Symbol('\\rbracklrtick', '\u298E'));
        },
    },
    loader: {load: ['[tex]/mathtools']},
    tex: {
        packages: {'[+]': ['mathtools'] },
        macros: %s,
        mathtools: {
            pairedDelimiters: %s,
        },
    },
    chtml: %s
}
''' % (json.dumps(macros), json.dumps(pairedDelimiters), json.dumps(chtml))

mathjax_options = { 'priority': 1000 }

html_js_files = [
    (
        None, {
            'body': mathjax_config_script,
            'priority': 1000
        }
    )
]

# Custom extensions

class InferenceDirective(Directive):
    r'''An inference rule with a single divider line.

    The directive takes 1 argument: the title of the inference rule.

    The premisses are newline separated before a divider liner. The divider
    line contains 3 or more -, optionally space separated. The final line is
    the conclusion of the inference rule. The premisses and conclusion are all
    parsed in math mode.

    Example:
      .. inference:: weak

         \Gamma \vdash t
         ---
         \Gamma, u \vdash t
    '''

    has_content = True
    required_arguments = 1
    optional_arguments = 0
    final_argument_whitespace = True

    def run(self):
        title = self.arguments[0]
        if '$' in title:
            title_math_node = nodes.math(title, r'\textbf{%s}' % title)
            title_node = nodes.inline('', '', title_math_node,
                                      classes=['inference-title'])
        else:
            title_node = nodes.inline(title, title,
                                      classes=['inference-title'])
        premisses = []
        conclusion = ''
        label = ''
        for i in range(len(self.content)):
            line = self.content[i]
            divider_match = re.fullmatch(' *---+( +.*)?', line)
            if divider_match:
                # i is now the index of the divider. Any following non-blank
                # lines are the conclusion. Latex does not like blank lines
                # inside of inline-math mode, so remove them.
                conclusion = '\n'.join(line for line in self.content[i + 1:]
                                       if line.strip())
                label = divider_match.group(1) or ''
                break
            premisses.append(line)
        latex = '\\dfrac{\n%s\n}{\n%s\n} %s' % ('\\qquad\n'.join(premisses),
                                             conclusion, label)
        math_node = nodes.math(latex, latex)
        line_block = nodes.line_block('', nodes.line('', '', title_node),
                                      nodes.line('', '', math_node))
        return [line_block]

class DefinitionDirective(Directive):
    r'''A math definition with a title

    The directive takes 1 argument: the title of the definition rule.

    The first line of the body is the definendum, the second line should
    contain three or more equal signs, and the third line is the defineins. The
    definedum and difineins are parsed in latex math mode.

    Example:
      .. definition:: context-inclusion

         \Wfc{\Gamma} \subseteq \Wfc{\Gamma'}
         ===
         \Wfc{\Gamma'} \to \Bigl(\Wfc{\Gamma} \times \Gamma \subseteq \Gamma' \Bigr)
    '''

    has_content = True
    required_arguments = 0
    optional_arguments = 1
    final_argument_whitespace = True

    def run(self):
        if len(self.content) != 3:
            raise self.error("The definition directive should contain exactly "
                             "3 lines. Actual: %d" % len(self.content))
        definendum = self.content[0]
        if not re.fullmatch(' *===+ *', self.content[1]):
            raise self.warning("The second line should contain 3 or more "
                               "equal signs by themselves.")
        defineins = self.content[2]
        latex = r'%s \enspace \triangleq \enspace %s' % (definendum, defineins)
        math_node = nodes.math(latex, latex, nowrap=False, number=None)

        if len(self.arguments) > 0:
            title = self.arguments[0]
            if '$' in title:
                title_math_node = nodes.math(title, r'\textbf{%s}' % title)
                title_node = nodes.inline('', '', title_math_node,
                                          classes=['inference-title'])
            else:
                title_node = nodes.inline(title, title,
                                          classes=['inference-title'])
            line_block = nodes.line_block('', nodes.line('', '', title_node),
                                          nodes.line('', '', math_node))
            return [line_block]
        else:
            return [math_node]

def setup(app):
    app.add_directive('inference', InferenceDirective)
    app.add_directive('definition', DefinitionDirective)
