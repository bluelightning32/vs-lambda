from collections import namedtuple

from docutils import nodes

from sphinx import addnodes
from sphinx.util import logging
from sphinx.util.docutils import SphinxDirective

log = logging.getLogger('conf')

GridMap = namedtuple('GridMap', ['parents', 'map', 'docname'])

class GridMapDirective(SphinxDirective):
    has_content = True
    required_arguments = 1
    optional_arguments = 100

    def run(self):
        name = ' '.join(self.arguments).strip()
        parents = []
        if ':' in name:
            name, parents = name.split(':', 2)
            name = name.strip()
            parents = [parent.strip() for parent in parents.split(',')]
        if not name.isidentifier():
            raise self.error("Grid map name '%s' is not a valid identifier." % name)
        for parent in parents:
            if not parent.isidentifier():
                raise self.error("Grid map parent '%s' is not a valid identifier." % parent)

        map = { }
        for line in self.content:
            remaining = line.strip()
            if not line:
                # Skip blank lines
                continue
            if remaining[0] == '#':
                # Skip comment lines
                continue

            if remaining[0] == "'":
                # Quoted character string
                if remaining[2] != "'":
                    raise self.error("Quoted character strings must end with ' and contain exactly 1 character.")
                char = remaining[1]
                remaining = remaining[3:]
            else:
                # Unquoted character
                char = remaining[0]
                remaining = remaining[1:]
            remaining = remaining.strip()
            if remaining[0] != ':':
                raise self.error("Expected a colon instead of '%s'." % remaining)
            remaining = remaining[1:]

            if not remaining:
                raise self.error("Filename missing for character '%s'." % char)

            if remaining[0] == "'":
                parts = remaining[1:].split("'", 1)
                if len(parts) < 2:
                    raise self.error("Missing terminating ' on filename '%s'." % remaining)
                if len(parts[1]) and parts[1][0] != '#':
                    raise self.error('Only a comment may follow the filename.')
                filename = parts[0]
            else:
                filename = remaining.split('#', 1)[0]
                filename = filename.strip()
            if char in map:
                raise self.error("Grid map '%s' already defined character '%s'.", name, char)
            map[char] = filename

        if not hasattr(self.env, 'grid_maps'):
            self.env.grid_maps = {}
        self.env.grid_maps[name] = GridMap(parents, map, self.env.docname)
        return list()

    @classmethod
    def purge_old(cls, app, env, docname):
        if not hasattr(env, 'grid_maps'):
            return
        env.grid_maps = {name:m for (name, m) in env.grid_maps.items() if m.docname != docname}

    @classmethod
    def merge(cls, app, env, docnames, other):
        if not hasattr(env, 'grid_maps'):
            env.grid_maps = {}
        if not hasattr(other, 'grid_maps'):
            other.grid_maps = {}
        for name in env.grid_maps.keys() & other.grid_maps.keys():
            raise KeyError("Key '%s' appears in multiple files." % name)
        env.grid_maps.update(other.grid_maps)


class ImageGrid(SphinxDirective):
    has_content = True
    required_arguments = 1
    optional_arguments = 100
    final_argument_whitespace = False

    def run(self):
        parents = self.arguments
        cols = None
        rows = []
        for line in self.content:
            line = line.strip()
            if not line:
                # Skip blank lines
                continue
            if line[0] == '#':
                # Skip comments
                continue
            if line[0] != '|':
                raise self.error("Invalid line: '%s'. Non-blank, non-comment image grid lines must start with |." % line)
            if line[-1] != '|':
                raise self.error('Non-blank, non-comment image grid lines must end with |.')
            if cols is None:
                cols = len(line) - 2
            if len(line) != cols + 2:
                raise self.error('All image grid rows must have the same number of columns.')
            rows.append(line[1:-1])

        table_spec = addnodes.tabular_col_spec(spec=' '.join(['@{}l@{}']*cols))

        table = nodes.table('', classes=['longtable', 'borderless'])
        group = nodes.tgroup('', cols=cols)
        table.append(group)
        for i in range(cols):
            group.append(nodes.colspec('', colwidth=1))
        body = nodes.tbody('')
        group.append(body)

        for row in rows:
            trow = nodes.row('')
            for c in row:
                filename = None
                for parent in parents:
                    if c in self.env.grid_maps[parent].map:
                        filename = self.env.relfn2path(self.env.grid_maps[parent].map[c],
                                                       self.env.grid_maps[parent].docname)[0]
                        break
                if filename is None:
                    raise self.error("Unable to find character '%s' in parent grid maps." % c)
                col_contents = nodes.inline('', '',
                                            nodes.raw('', r'\raisebox{-\dp\strutbox}{', format='latex'),
                                            nodes.image(uri=filename, height='3em', classes=['image-grid']),
                                            nodes.raw('', r'}', format='latex'))
                trow.append(nodes.entry('', col_contents))
            body.append(trow)
        #self.env.note_reread()
        return [table_spec, table]

def setup(app):
    app.add_directive('image-grid', ImageGrid)
    app.add_directive('grid-map', GridMapDirective)
    app.connect('env-purge-doc', GridMapDirective.purge_old)
    app.connect('env-merge-info', GridMapDirective.merge)

    return {
        'version': '1.0',
        'parallel_read_safe': True,
        'parallel_write_safe': True,
    }
