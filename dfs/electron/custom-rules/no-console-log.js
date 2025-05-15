/** @type {import("eslint").Rule.RuleModule} */
export default {
  meta: {
    type: "problem",
    docs: {
      description: "Disallow console.log statements",
    },
    messages: {
      noConsoleLog: "Unexpected console.log statement.",
    },
    schema: [], // no options
  },
  create(context) {
    return {
      MemberExpression(node) {
        if (
          node.object.name === "console" &&
          node.property.name === "log"
        ) {
          context.report({
            node,
            messageId: "noConsoleLog",
          });
        }
      },
    };
  },
};
