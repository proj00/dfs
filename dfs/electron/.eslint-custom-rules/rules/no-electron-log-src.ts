import { TSESTree, ESLintUtils } from '@typescript-eslint/utils';

const createRule = ESLintUtils.RuleCreator((name) => {
  return `https://example.com/rule/${name}`;
});

type MessageIds = 'noFoo';
type Options = [];

export default createRule<Options, MessageIds>({
  name: 'no-foo',
  meta: {
    type: 'problem',
    docs: {
      description: 'Disallow the use of identifier named "foo".',
    },
    messages: {
      noFoo: 'Using "foo" as an identifier is not allowed.',
    },
    schema: [],
  },
  defaultOptions: [],
  create(context) {
    return {
      Identifier(node: TSESTree.Identifier) {
        if (node.name === 'foo') {
          context.report({ node, messageId: 'noFoo' });
        }
      },
    };
  },
});
